using System.Text;
using Microsoft.Extensions.Configuration;
using PrimeTect.Application.Abstractions;
using PrimeTect.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class PhotoNormalizationTests
{
    [Fact]
    public async Task Normalizes_all_eight_exif_orientations_to_the_same_upright_pixels()
    {
        var root = CreateRoot();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MeterPhotos:RootPath"] = root }).Build();
        var storage = new LocalMeterPhotoStorage(configuration);
        try
        {
            var reference = await StoreAsync(storage, WithJpegOrientation(CreateQuadrants(SKEncodedImageFormat.Jpeg), 1), "image/jpeg");
            var expected = await ReadBitmapAsync(storage, reference.StorageKey);
            for (ushort orientation = 2; orientation <= 8; orientation++)
            {
                var stored = await StoreAsync(storage, WithJpegOrientation(CreateQuadrants(SKEncodedImageFormat.Jpeg), orientation), "image/jpeg");
                using var actual = await ReadBitmapAsync(storage, stored.StorageKey);
                Assert.Equal(expected.Width, actual.Width);
                Assert.Equal(expected.Height, actual.Height);
                AssertSimilar(expected.GetPixel(8, 8), actual.GetPixel(8, 8));
                AssertSimilar(expected.GetPixel(expected.Width - 8, 8), actual.GetPixel(actual.Width - 8, 8));
                AssertSimilar(expected.GetPixel(8, expected.Height - 8), actual.GetPixel(8, actual.Height - 8));
                AssertSimilar(expected.GetPixel(expected.Width - 8, expected.Height - 8), actual.GetPixel(actual.Width - 8, actual.Height - 8));
            }
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task Supports_jpeg_png_and_webp_and_strips_exif_metadata(SKEncodedImageFormat format, string contentType)
    {
        var root = CreateRoot();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MeterPhotos:RootPath"] = root }).Build();
        var storage = new LocalMeterPhotoStorage(configuration);
        try
        {
            var source = CreateQuadrants(format);
            if (format == SKEncodedImageFormat.Jpeg)
            {
                source = WithJpegOrientation(source, 6);
            }

            var stored = await StoreAsync(storage, source, contentType);
            await using var opened = await storage.OpenReadAsync(stored.StorageKey, CancellationToken.None);
            Assert.NotNull(opened);
            using var bytes = new MemoryStream();
            await opened!.CopyToAsync(bytes);
            Assert.DoesNotContain(Encoding.ASCII.GetBytes("Exif\0\0"), bytes.ToArray());
            using var codec = SKCodec.Create(new MemoryStream(bytes.ToArray(), writable: false));
            Assert.NotNull(codec);
            Assert.Equal(SKEncodedOrigin.TopLeft, codec!.EncodedOrigin);
            Assert.Equal(stored.Width, codec.Info.Width);
            Assert.Equal(stored.Height, codec.Info.Height);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Rejects_corrupt_oversized_excessive_dimension_and_heic_inputs()
    {
        var root = CreateRoot();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MeterPhotos:RootPath"] = root }).Build();
        var storage = new LocalMeterPhotoStorage(configuration);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => StoreAsync(storage, new byte[] { 1, 2, 3 }, "image/jpeg"));
            await Assert.ThrowsAsync<InvalidDataException>(() => StoreAsync(storage, new byte[10 * 1024 * 1024 + 1], "image/jpeg"));
            await Assert.ThrowsAsync<InvalidDataException>(() => StoreAsync(storage, PngHeader(9000, 9000), "image/png"));
            await Assert.ThrowsAsync<InvalidDataException>(() => StoreAsync(storage, Encoding.ASCII.GetBytes("\0\0\0\x18ftypheic"), "image/heic"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static async Task<StoredMeterPhoto> StoreAsync(LocalMeterPhotoStorage storage, byte[] bytes, string contentType)
    {
        await using var input = new MemoryStream(bytes);
        return await storage.StoreAsync(input, contentType, "synthetic-meter.jpg", CancellationToken.None);
    }

    private static async Task<SKBitmap> ReadBitmapAsync(LocalMeterPhotoStorage storage, string key)
    {
        await using var opened = await storage.OpenReadAsync(key, CancellationToken.None);
        Assert.NotNull(opened);
        using var bytes = new MemoryStream();
        await opened!.CopyToAsync(bytes);
        return SKBitmap.Decode(bytes.ToArray()) ?? throw new InvalidOperationException("Synthetic normalized photo did not decode.");
    }

    private static byte[] CreateQuadrants(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 48, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { IsAntialias = false })
        {
            paint.Color = SKColors.Red;
            canvas.DrawRect(new SKRect(0, 0, 32, 24), paint);
            paint.Color = SKColors.Green;
            canvas.DrawRect(new SKRect(32, 0, 64, 24), paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(new SKRect(0, 24, 32, 48), paint);
            paint.Color = SKColors.Yellow;
            canvas.DrawRect(new SKRect(32, 24, 64, 48), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return encoded?.ToArray() ?? throw new InvalidOperationException("Synthetic image encoding failed.");
    }

    private static byte[] WithJpegOrientation(byte[] jpeg, ushort orientation)
    {
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            throw new ArgumentException("Expected a JPEG.", nameof(jpeg));
        }

        var exif = new byte[32];
        Encoding.ASCII.GetBytes("Exif\0\0").CopyTo(exif, 0);
        exif[6] = (byte)'I';
        exif[7] = (byte)'I';
        exif[8] = 0x2A;
        exif[10] = 0x08;
        exif[14] = 0x01;
        exif[16] = 0x12;
        exif[17] = 0x01;
        exif[18] = 0x03;
        exif[22] = 0x01;
        exif[24] = (byte)orientation;
        exif[25] = (byte)(orientation >> 8);
        using var result = new MemoryStream();
        result.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x22 });
        result.Write(exif);
        result.Write(jpeg, 2, jpeg.Length - 2);
        return result.ToArray();
    }

    private static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[32];
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47; bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;
        bytes[12] = (byte)'I'; bytes[13] = (byte)'H'; bytes[14] = (byte)'D'; bytes[15] = (byte)'R';
        bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16); bytes[18] = (byte)(width >> 8); bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16); bytes[22] = (byte)(height >> 8); bytes[23] = (byte)height;
        return bytes;
    }

    private static void AssertSimilar(SKColor expected, SKColor actual)
    {
        Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, 70);
        Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, 70);
        Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, 70);
    }

    private static string CreateRoot() => Path.Combine(Path.GetTempPath(), $"primetect-photo-normalization-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
