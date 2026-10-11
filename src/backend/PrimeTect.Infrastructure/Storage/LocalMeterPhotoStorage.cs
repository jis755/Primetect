using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Configuration;
using PrimeTect.Application.Abstractions;
using SkiaSharp;

namespace PrimeTect.Infrastructure.Storage;

public sealed class LocalMeterPhotoStorage(IConfiguration configuration) : IMeterPhotoStorage
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private const int MaxDimension = 8000;
    private const int MinDimension = 32;
    private const long MaxDecodedPixels = 25_000_000;
    private static readonly TimeSpan MaxProcessingTime = TimeSpan.FromSeconds(10);

    private string RootPath => configuration["MeterPhotos:RootPath"] is { Length: > 0 } configured
        ? Path.GetFullPath(configured)
        : Path.Combine(AppContext.BaseDirectory, "App_Data", "meter-photos");

    public async Task<StoredMeterPhoto> StoreAsync(Stream content, string? contentType, string? originalFileName, CancellationToken cancellationToken)
    {
        var bytes = await ReadBoundedAsync(content, cancellationToken);
        var inspected = ImageFileInspector.Inspect(bytes);
        if (!string.IsNullOrWhiteSpace(contentType) && !string.Equals(contentType, inspected.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The file content does not match a supported image type.");
        }

        using var processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processingCts.CancelAfter(MaxProcessingTime);
        var processingToken = processingCts.Token;
        if (inspected.Width > 0 && inspected.Height > 0)
        {
            ValidateDimensions(inspected, processingToken);
        }

        NormalizedPhoto normalized;
        try
        {
            normalized = await Task.Run(() => Normalize(bytes, inspected, processingToken), processingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new InvalidDataException("The image is corrupt, unsupported, or could not be normalized safely.");
        }

        var dimensions = new InspectedImage(inspected.ContentType, inspected.Extension, normalized.Width, normalized.Height);
        ValidateDimensions(dimensions, processingToken);
        if (normalized.Bytes.Length == 0 || normalized.Bytes.Length > MaxBytes)
        {
            throw new InvalidDataException("The normalized meter photo exceeds the 10 MB limit.");
        }

        Directory.CreateDirectory(RootPath);
        var key = $"{Guid.NewGuid():N}{dimensions.Extension}";
        var path = GetSafePath(key);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await output.WriteAsync(normalized.Bytes, processingToken);
        }

        return new StoredMeterPhoto(key, dimensions.ContentType, dimensions.Extension, normalized.Bytes.Length, dimensions.Width, dimensions.Height, CleanFileName(originalFileName));
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = GetSafePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = GetSafePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanRead)
        {
            throw new InvalidDataException("The meter photo could not be read.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxBytes)
            {
                throw new InvalidDataException("The meter photo is larger than the 10 MB limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            throw new InvalidDataException("The meter photo is empty.");
        }

        return buffer.ToArray();
    }

    private static NormalizedPhoto Normalize(byte[] bytes, InspectedImage signature, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var input = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("The image is corrupt or could not be decoded safely.");
        if (codec.FrameCount > 1)
        {
            throw new InvalidDataException("Animated meter photos are not supported.");
        }

        var info = codec.Info;
        var sourceInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        ValidateDimensions(new InspectedImage(signature.ContentType, signature.Extension, sourceInfo.Width, sourceInfo.Height), cancellationToken);
        using var source = new SKBitmap(sourceInfo);
        if (codec.GetPixels(sourceInfo, source.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidDataException("The image is corrupt or could not be decoded safely.");
        }

        var origin = codec.EncodedOrigin;
        var swapDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var targetWidth = swapDimensions ? source.Height : source.Width;
        var targetHeight = swapDimensions ? source.Width : source.Height;
        using var target = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(CreateOrientationMatrix(origin, source.Width, source.Height));
            canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
            canvas.Flush();
        }

        using var image = SKImage.FromBitmap(target);
        using var encoded = image.Encode(ToFormat(signature.ContentType), signature.ContentType == "image/jpeg" ? 92 : 90)
            ?? throw new InvalidDataException("The normalized image could not be encoded safely.");
        cancellationToken.ThrowIfCancellationRequested();
        return new NormalizedPhoto(encoded.ToArray(), target.Width, target.Height);
    }

    private static SKEncodedImageFormat ToFormat(string contentType) => contentType switch
    {
        "image/jpeg" => SKEncodedImageFormat.Jpeg,
        "image/png" => SKEncodedImageFormat.Png,
        "image/webp" => SKEncodedImageFormat.Webp,
        _ => throw new InvalidDataException("Use a valid JPEG, PNG, or WebP image. HEIC is not supported."),
    };

    private static SKMatrix CreateOrientationMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopLeft => new SKMatrix(1, 0, 0, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => new SKMatrix(1, 0, 0, 0, 1, 0, 0, 0, 1),
    };

    private static InspectedImage ValidateDimensions(InspectedImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (image.Width <= 0 || image.Height <= 0 || image.Width < MinDimension || image.Height < MinDimension || image.Width > MaxDimension || image.Height > MaxDimension || (long)image.Width * image.Height > MaxDecodedPixels)
        {
            throw new InvalidDataException($"The meter photo dimensions must be between {MinDimension} and {MaxDimension} pixels and no more than {MaxDecodedPixels:N0} decoded pixels.");
        }

        return image;
    }

    private string GetSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || storageKey.Contains(Path.DirectorySeparatorChar) || storageKey.Contains(Path.AltDirectorySeparatorChar) || Path.GetFileName(storageKey) != storageKey)
        {
            throw new InvalidDataException("The photo storage identifier is invalid.");
        }

        var root = Path.GetFullPath(RootPath);
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The photo storage identifier is invalid.");
        }

        return path;
    }

    private static string? CleanFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var clean = Path.GetFileName(fileName.Trim());
        return clean.Length > 120 ? clean[..120] : clean;
    }
}

internal sealed record NormalizedPhoto(byte[] Bytes, int Width, int Height);
internal sealed record InspectedImage(string ContentType, string Extension, int Width, int Height);

internal static class ImageFileInspector
{
    public static InspectedImage Inspect(byte[] bytes)
    {
        try
        {
            if (bytes.Length >= 32 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A && ReadUInt32BigEndian(bytes, 12) == 0x49484452)
            {
                return new InspectedImage("image/png", ".png", checked((int)ReadUInt32BigEndian(bytes, 16)), checked((int)ReadUInt32BigEndian(bytes, 20)));
            }

            if (bytes.Length >= 12 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            {
                return new InspectedImage("image/webp", ".webp", 0, 0);
            }

            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return InspectJpeg(bytes);
            }

            if (LooksLikeHeic(bytes))
            {
                throw new InvalidDataException("HEIC images are not supported. Use JPEG, PNG, or WebP.");
            }

            throw new InvalidDataException("Use a valid JPEG, PNG, or WebP image. HEIC is not supported.");
        }
        catch (OverflowException)
        {
            throw new InvalidDataException("The image dimensions are too large to process safely.");
        }
    }

    private static InspectedImage InspectJpeg(byte[] bytes)
    {
        var index = 2;
        while (index + 9 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
            {
                index++;
                continue;
            }

            while (index < bytes.Length && bytes[index] == 0xFF) index++;
            if (index >= bytes.Length) break;
            var marker = bytes[index++];
            if (marker is 0xD8 or 0xD9) continue;
            if (index + 1 >= bytes.Length) break;
            var length = (bytes[index] << 8) | bytes[index + 1];
            if (length < 2 || index + length > bytes.Length) break;
            if ((marker is >= 0xC0 and <= 0xC3) || (marker is >= 0xC5 and <= 0xC7) || (marker is >= 0xC9 and <= 0xCB) || (marker is >= 0xCD and <= 0xCF))
            {
                return new InspectedImage("image/jpeg", ".jpg", (bytes[index + 5] << 8) | bytes[index + 6], (bytes[index + 3] << 8) | bytes[index + 4]);
            }

            index += length;
        }

        throw new InvalidDataException("The JPEG header is incomplete or invalid.");
    }

    private static bool LooksLikeHeic(byte[] bytes)
    {
        if (bytes.Length < 12 || bytes[4] != (byte)'f' || bytes[5] != (byte)'t' || bytes[6] != (byte)'y' || bytes[7] != (byte)'p')
        {
            return false;
        }

        var brand = System.Text.Encoding.ASCII.GetString(bytes, 8, Math.Min(4, bytes.Length - 8));
        return brand is "heic" or "heix" or "hevc" or "hevx" or "mif1";
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int index) =>
        ((uint)bytes[index] << 24) | ((uint)bytes[index + 1] << 16) | ((uint)bytes[index + 2] << 8) | bytes[index + 3];
}

public sealed class UnavailableMeterOcrProvider : IMeterOcrProvider
{
    public string ProviderName => "unavailable-local-provider";

    public Task<MeterOcrSuggestion> SuggestAsync(Stream photo, CancellationToken cancellationToken) =>
        Task.FromResult(new MeterOcrSuggestion(null, "Unavailable", ProviderName, null, "Local OCR is not configured; confirm the reading manually."));
}
