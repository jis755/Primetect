using System.Buffers.Binary;
using Microsoft.Extensions.Configuration;
using PrimeTect.Domain.Entities;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class StepTwoComponentTests
{
    [Fact]
    public void Meter_qr_rotation_revokes_the_previous_navigation_reference_without_deleting_history()
    {
        var organizationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var first = new MeterQrLink(organizationId, propertyId, unitId, actorId, "first-token-hash", MeterQrPlacement.ElectricityMeter);
        var createdAt = first.CreatedAtUtc;

        first.Revoke(actorId);
        first.Revoke(Guid.NewGuid());

        Assert.False(first.IsActive);
        Assert.Equal(createdAt, first.CreatedAtUtc);
        Assert.NotNull(first.RevokedAtUtc);
    }

    [Fact]
    public void Meter_photo_keeps_the_suggestion_separate_from_the_confirmed_reading()
    {
        var photo = new MeterPhoto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "safe-photo-key.png",
            "image/png",
            ".png",
            1024,
            640,
            480,
            "meter.png",
            "Suggested",
            "test-provider-1",
            10040m,
            0.91m);

        photo.Attach(Guid.NewGuid(), 10041m);

        Assert.Equal(MeterPhotoStatus.Attached, photo.Status);
        Assert.Equal(10040m, photo.OcrSuggestion);
        Assert.Equal(10041m, photo.ConfirmedReading);
        Assert.NotEqual(photo.ConfirmedByUserId, photo.UploadedByUserId);
    }

    [Fact]
    public async Task Local_photo_storage_validates_signature_type_dimensions_and_safe_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"primetect-step2-photo-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MeterPhotos:RootPath"] = root })
            .Build();
        var storage = new LocalMeterPhotoStorage(configuration);
        try
        {
            await using var image = new MemoryStream(MinimalPng(640, 480));
            var stored = await storage.StoreAsync(image, "image/png", "meter.png", CancellationToken.None);

            Assert.Equal("image/png", stored.ContentType);
            Assert.Equal(640, stored.Width);
            Assert.Equal(480, stored.Height);
            Assert.DoesNotContain("..", stored.StorageKey, StringComparison.Ordinal);
            await using (var opened = await storage.OpenReadAsync(stored.StorageKey, CancellationToken.None))
            {
                Assert.NotNull(opened);
            }

            await using var mismatched = new MemoryStream(MinimalPng(640, 480));
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.StoreAsync(mismatched, "image/jpeg", "meter.png", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.OpenReadAsync("../outside.png", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static byte[] MinimalPng(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
