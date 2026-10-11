using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PrimeTect.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class LocalTesseractMeterOcrProviderTests
{
    [Fact]
    public async Task Real_engine_reads_a_single_centered_display_and_ignores_outer_labels()
    {
        var result = await SuggestAsync(CreateMeterImage("12345", "998877", "220V"));

        Assert.True(result.Outcome == "Suggested", $"{result.Outcome}: {result.Explanation}");
        Assert.Equal(12345m, result.SuggestedReading);
        Assert.Equal("tesseract-5.5.0-eng-fast", result.ProviderVersion);
        Assert.NotNull(result.Confidence);
    }

    [Fact]
    public async Task Real_engine_preserves_an_explicit_decimal()
    {
        var result = await SuggestAsync(CreateMeterImage("1234.5", null, null));

        Assert.Equal("Suggested", result.Outcome);
        Assert.Equal(1234.5m, result.SuggestedReading);
    }

    [Fact]
    public async Task Real_engine_rejects_multiple_display_candidates()
    {
        var result = await SuggestAsync(CreateMeterImage("12345 67890", null, null));

        Assert.Null(result.SuggestedReading);
        Assert.Contains(result.Outcome, new[] { "Ambiguous", "NoReadingDetected" });
    }

    [Fact]
    public async Task Blank_image_has_no_suggestion()
    {
        var result = await SuggestAsync(CreateMeterImage(null, null, null));

        Assert.Null(result.SuggestedReading);
        Assert.Equal("NoReadingDetected", result.Outcome);
    }

    [Fact]
    public async Task Missing_engine_or_data_leaves_manual_entry_available()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MeterOcr:ExecutablePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "tesseract.exe"),
            ["MeterOcr:TessdataPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        }).Build();
        var provider = new LocalTesseractMeterOcrProvider(configuration, NullLogger<LocalTesseractMeterOcrProvider>.Instance);
        await using var image = new MemoryStream(CreateMeterImage("12345", null, null));

        var result = await provider.SuggestAsync(image, CancellationToken.None);

        Assert.Equal("Unavailable", result.Outcome);
        Assert.Null(result.SuggestedReading);
    }

    [Theory]
    [InlineData("১২৩৪.৫", "1234.5")]
    [InlineData("12৩4", "1234")]
    public void Bangla_digit_normalization_is_parsing_only(string source, string expected) =>
        Assert.Equal(expected, LocalTesseractMeterOcrProvider.NormalizeDigits(source));

    private static async Task<PrimeTect.Application.Abstractions.MeterOcrSuggestion> SuggestAsync(byte[] image)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var provider = new LocalTesseractMeterOcrProvider(new ConfigurationBuilder().Build(), loggerFactory.CreateLogger<LocalTesseractMeterOcrProvider>());
        await using var stream = new MemoryStream(image);
        return await provider.SuggestAsync(stream, CancellationToken.None);
    }

    private static byte[] CreateMeterImage(string? display, string? topLabel, string? bottomLabel)
    {
        using var bitmap = new SKBitmap(1200, 800, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.LightGray);
        using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial"), 58);
        if (topLabel is not null) canvas.DrawText(topLabel, 70, 100, SKTextAlign.Left, labelFont, labelPaint);
        if (bottomLabel is not null) canvas.DrawText(bottomLabel, 70, 760, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawRoundRect(new SKRect(180, 230, 1020, 570), 20, 20, new SKPaint { Color = SKColors.Black });
        if (display is not null)
        {
            using var displayPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var displayFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 185);
            canvas.DrawText(display, 235, 465, SKTextAlign.Left, displayFont, displayPaint);
        }
        using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
