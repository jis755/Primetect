using Microsoft.Extensions.Configuration;
using PrimeTect.Application.Abstractions;

namespace PrimeTect.Infrastructure.Storage;

public sealed class LocalMeterPhotoStorage(IConfiguration configuration) : IMeterPhotoStorage
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private const int MaxDimension = 8000;
    private const int MinDimension = 32;

    private string RootPath => configuration["MeterPhotos:RootPath"] is { Length: > 0 } configured
        ? Path.GetFullPath(configured)
        : Path.Combine(AppContext.BaseDirectory, "App_Data", "meter-photos");

    public async Task<StoredMeterPhoto> StoreAsync(Stream content, string? contentType, string? originalFileName, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > MaxBytes)
        {
            throw new InvalidDataException("The meter photo is empty or larger than the 10 MB limit.");
        }

        buffer.Position = 0;
        var inspected = ImageFileInspector.Inspect(buffer);
        if (inspected.Width < MinDimension || inspected.Height < MinDimension || inspected.Width > MaxDimension || inspected.Height > MaxDimension)
        {
            throw new InvalidDataException($"The meter photo dimensions must be between {MinDimension} and {MaxDimension} pixels.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !string.Equals(contentType, inspected.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The file content does not match a supported image type.");
        }

        Directory.CreateDirectory(RootPath);
        var key = $"{Guid.NewGuid():N}{inspected.Extension}";
        var path = GetSafePath(key);
        buffer.Position = 0;
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            await buffer.CopyToAsync(output, cancellationToken);
        }

        return new StoredMeterPhoto(key, inspected.ContentType, inspected.Extension, buffer.Length, inspected.Width, inspected.Height, CleanFileName(originalFileName));
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

internal sealed record InspectedImage(string ContentType, string Extension, int Width, int Height);

internal static class ImageFileInspector
{
    public static InspectedImage Inspect(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var bytes = copy.ToArray();
        if (bytes.Length >= 32 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A && ReadUInt32BigEndian(bytes, 12) == 0x49484452)
        {
            return new InspectedImage("image/png", ".png", checked((int)ReadUInt32BigEndian(bytes, 16)), checked((int)ReadUInt32BigEndian(bytes, 20)));
        }

        if (bytes.Length >= 30 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
        {
            return InspectWebp(bytes);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return InspectJpeg(bytes);
        }

        throw new InvalidDataException("Use a valid JPEG, PNG, or WebP image. HEIC is not supported.");
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

    private static InspectedImage InspectWebp(byte[] bytes)
    {
        if (bytes[12] == (byte)'V' && bytes[13] == (byte)'P' && bytes[14] == (byte)'8' && bytes[15] == (byte)'X' && bytes.Length >= 30)
        {
            var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            return new InspectedImage("image/webp", ".webp", width, height);
        }

        throw new InvalidDataException("This WebP variant does not expose safe dimensions for validation.");
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
