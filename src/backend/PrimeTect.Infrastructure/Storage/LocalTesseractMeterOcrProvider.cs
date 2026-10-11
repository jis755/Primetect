using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PrimeTect.Application.Abstractions;
using SkiaSharp;

namespace PrimeTect.Infrastructure.Storage;

/// <summary>Runs packaged Windows x64 Tesseract on a temporary crop; it never alters stored photo evidence.</summary>
public sealed class LocalTesseractMeterOcrProvider(IConfiguration configuration, ILogger<LocalTesseractMeterOcrProvider> logger) : IMeterOcrProvider
{
    private const int MaxDurationMilliseconds = 8_000;
    private static readonly SemaphoreSlim Jobs = new(2, 2);
    private static readonly Regex NumberPattern = new(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public string ProviderName => "tesseract-5.5.0-eng-fast";

    public async Task<MeterOcrSuggestion> SuggestAsync(Stream photo, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return Unavailable("Local OCR is packaged and verified only for Windows x64; confirm the reading manually.");
        var executable = configuration["MeterOcr:ExecutablePath"] ?? Path.Combine(AppContext.BaseDirectory, "x64", "tesseract.exe");
        var dataPath = configuration["MeterOcr:TessdataPath"] ?? Path.Combine(AppContext.BaseDirectory, "Ocr", "tessdata");
        if (!File.Exists(executable) || !File.Exists(Path.Combine(dataPath, "eng.traineddata"))) return Unavailable("Local OCR engine or English data is unavailable; confirm the reading manually.");
        try { await Jobs.WaitAsync(cancellationToken); }
        catch (OperationCanceledException) { return Unavailable("OCR was cancelled; confirm the reading manually."); }

        var temp = Path.Combine(configuration["MeterOcr:TemporaryPath"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "meter-ocr-temp"), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var input = Path.Combine(temp, "display.png");
            await WriteDisplayCropAsync(photo, input, cancellationToken);
            return await RunTesseractAsync(executable, dataPath, input, temp, cancellationToken);
        }
        catch (MultipleDisplayTokensException) { return new(null, "Ambiguous", ProviderName, null, "More than one possible display reading was detected. Retake the photo or enter the reading manually."); }
        catch (InvalidDataException) { return new(null, "NoReadingDetected", ProviderName, null, "The meter display region could not be read. Retake the photo or enter the reading manually."); }
        catch (OperationCanceledException) { return Unavailable("OCR was cancelled or timed out; confirm the reading manually."); }
        catch (Exception exception) { logger.LogWarning(exception, "Local meter OCR failed without changing a reading."); return Unavailable("Local OCR could not process this photo; confirm the reading manually."); }
        finally
        {
            Jobs.Release();
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
            catch (Exception exception) { logger.LogWarning(exception, "Could not remove temporary OCR derivative directory."); }
        }
    }

    private async Task<MeterOcrSuggestion> RunTesseractAsync(string executable, string dataPath, string inputPath, string temp, CancellationToken cancellationToken)
    {
        var output = Path.Combine(temp, "result");
        using var process = new Process { StartInfo = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true }, EnableRaisingEvents = true };
        process.StartInfo.ArgumentList.Add(inputPath); process.StartInfo.ArgumentList.Add(output);
        process.StartInfo.ArgumentList.Add("--tessdata-dir"); process.StartInfo.ArgumentList.Add(dataPath);
        process.StartInfo.ArgumentList.Add("-l"); process.StartInfo.ArgumentList.Add("eng");
        process.StartInfo.ArgumentList.Add("--oem"); process.StartInfo.ArgumentList.Add("1");
        process.StartInfo.ArgumentList.Add("--psm"); process.StartInfo.ArgumentList.Add("7");
        process.StartInfo.ArgumentList.Add("-c"); process.StartInfo.ArgumentList.Add("tessedit_char_whitelist=0123456789.");
        process.StartInfo.ArgumentList.Add("tsv");
        if (!process.Start()) return Unavailable("Local OCR could not start; confirm the reading manually.");
        using var timeout = new CancellationTokenSource(MaxDurationMilliseconds);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try { await process.WaitForExitAsync(linked.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            return Unavailable("OCR timed out or was cancelled; confirm the reading manually.");
        }
        if (process.ExitCode != 0 || !File.Exists(output + ".tsv"))
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            logger.LogWarning("Local meter OCR exited with code {ExitCode}: {Error}", process.ExitCode, error);
            return Unavailable("Local OCR could not read this photo; confirm the reading manually.");
        }

        var candidates = new List<(string Value, decimal Confidence)>();
        foreach (var row in await File.ReadAllLinesAsync(output + ".tsv", cancellationToken))
        {
            var fields = row.Split('\t');
            if (fields.Length < 12 || !int.TryParse(fields[0], out var level) || level != 5 || !decimal.TryParse(fields[10], NumberStyles.Number, CultureInfo.InvariantCulture, out var confidence)) continue;
            var text = NormalizeDigits(fields[11]).Trim();
            var matches = NumberPattern.Matches(text);
            if (matches.Count == 1 && matches[0].Value.Length == text.Length && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value >= 0) candidates.Add((text, confidence));
        }
        var distinct = candidates.Select(candidate => candidate.Value).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0) return new(null, "NoReadingDetected", ProviderName, null, "No unambiguous reading was detected in the display region. Retake the photo or enter the reading manually.");
        if (distinct.Length != 1 || candidates.Count != 1) return new(null, "Ambiguous", ProviderName, null, "More than one possible display reading was detected. Retake the photo or enter the reading manually.");
        if (!decimal.TryParse(candidates[0].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var suggested)) return new(null, "NoReadingDetected", ProviderName, null, "No valid reading was detected. Confirm the reading manually.");
        return new(suggested, "Suggested", ProviderName, Math.Clamp(candidates[0].Confidence / 100m, 0m, 1m), "Local OCR found one display-region candidate. Review or correct it, then confirm manually.");
    }

    private static async Task WriteDisplayCropAsync(Stream photo, string outputPath, CancellationToken cancellationToken)
    {
        using var bitmap = SKBitmap.Decode(photo) ?? throw new InvalidDataException("The normalized meter photo could not be decoded.");
        if (bitmap.Width < 160 || bitmap.Height < 120) throw new InvalidDataException("The meter display region is too small.");
        // Only the central display region is inspected, deliberately excluding typical top/bottom serial/date/voltage labels.
        var crop = new SKRectI(bitmap.Width * 16 / 100, bitmap.Height * 30 / 100, bitmap.Width * 84 / 100, bitmap.Height * 70 / 100);
        using var display = new SKBitmap(crop.Width, crop.Height, bitmap.ColorType, bitmap.AlphaType);
        if (!bitmap.ExtractSubset(display, crop)) throw new InvalidDataException("The meter display region could not be selected.");
        if (HasMultipleSeparatedTokens(display)) throw new MultipleDisplayTokensException();
        using var image = SKImage.FromBitmap(display); using var data = image.Encode(SKEncodedImageFormat.Png, 100); await using var output = File.Create(outputPath);
        await output.WriteAsync(data.ToArray(), cancellationToken);
    }

    private MeterOcrSuggestion Unavailable(string explanation) => new(null, "Unavailable", ProviderName, null, explanation);
    public static string NormalizeDigits(string value) => value.Replace('০', '0').Replace('১', '1').Replace('২', '2').Replace('৩', '3').Replace('৪', '4').Replace('৫', '5').Replace('৬', '6').Replace('৭', '7').Replace('৮', '8').Replace('৯', '9');

    private static bool HasMultipleSeparatedTokens(SKBitmap bitmap)
    {
        var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        var centerLuminance = (center.Red * 299 + center.Green * 587 + center.Blue * 114) / 1000;
        var occupied = new bool[bitmap.Width];
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = bitmap.Height / 8; y < bitmap.Height - bitmap.Height / 8; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var luminance = (pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000;
                if (Math.Abs(luminance - centerLuminance) > 80) { occupied[x] = true; break; }
            }
        }
        var groups = 0;
        var gap = 0;
        var inGroup = false;
        foreach (var isOccupied in occupied)
        {
            if (isOccupied)
            {
                if (!inGroup && (groups == 0 || gap >= Math.Max(40, bitmap.Width / 18))) groups++;
                inGroup = true; gap = 0;
            }
            else if (inGroup) { gap++; if (gap >= Math.Max(40, bitmap.Width / 18)) inGroup = false; }
        }
        return groups > 1;
    }

    private sealed class MultipleDisplayTokensException : Exception;
}
