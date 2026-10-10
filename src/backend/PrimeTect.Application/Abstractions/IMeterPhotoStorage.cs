namespace PrimeTect.Application.Abstractions;

public sealed record StoredMeterPhoto(
    string StorageKey,
    string ContentType,
    string FileExtension,
    long ByteLength,
    int Width,
    int Height,
    string? OriginalFileName);

public sealed record MeterOcrSuggestion(
    decimal? SuggestedReading,
    string Outcome,
    string? ProviderVersion,
    decimal? Confidence,
    string? Explanation);

public interface IMeterPhotoStorage
{
    Task<StoredMeterPhoto> StoreAsync(Stream content, string? contentType, string? originalFileName, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public interface IMeterOcrProvider
{
    string ProviderName { get; }
    Task<MeterOcrSuggestion> SuggestAsync(Stream photo, CancellationToken cancellationToken);
}
