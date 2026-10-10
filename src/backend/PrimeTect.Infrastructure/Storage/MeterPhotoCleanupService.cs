using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrimeTect.Application.Abstractions;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.Persistence;

namespace PrimeTect.Infrastructure.Storage;

public sealed class MeterPhotoCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<MeterPhotoCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Meter photo cleanup failed; staged uploads will be retried later.");
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PropertyManagementDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IMeterPhotoStorage>();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var photos = await db.MeterPhotos.Where(photo => photo.Status == MeterPhotoStatus.Staged && photo.UploadedAtUtc < cutoff).Take(100).ToListAsync(cancellationToken);
        foreach (var photo in photos)
        {
            await storage.DeleteAsync(photo.StorageKey, cancellationToken);
            photo.MarkOrphaned();
        }

        if (photos.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
