namespace PrimeTect.Domain.Entities;

/// <summary>
/// Audit of one committed opening-reading import. It records the actor, property, sanitized filename, file hash,
/// validated content hash, counts, and outcome only; it never stores raw file contents. The organization-scoped
/// submission key makes retries idempotent.
/// </summary>
public sealed class OpeningReadingImport
{
    private OpeningReadingImport()
    {
    }

    public OpeningReadingImport(
        Guid organizationId,
        Guid propertyId,
        Guid actorUserId,
        string fileName,
        string fileHash,
        string contentHash,
        int rowCount,
        int importedCount,
        string outcome,
        Guid submissionKey)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        ActorUserId = actorUserId;
        FileName = fileName;
        FileHash = fileHash;
        ContentHash = contentHash;
        RowCount = rowCount;
        ImportedCount = importedCount;
        Outcome = outcome;
        SubmissionKey = submissionKey;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string FileHash { get; private set; } = string.Empty;

    public string ContentHash { get; private set; } = string.Empty;

    public int RowCount { get; private set; }

    public int ImportedCount { get; private set; }

    public string Outcome { get; private set; } = string.Empty;

    public Guid SubmissionKey { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
