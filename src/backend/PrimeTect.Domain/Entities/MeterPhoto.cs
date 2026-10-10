using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class MeterPhoto
{
    private MeterPhoto()
    {
    }

    public MeterPhoto(
        Guid organizationId,
        Guid propertyId,
        Guid billingCycleId,
        Guid billingUnitBillId,
        Guid unitId,
        Guid uploadedByUserId,
        string storageKey,
        string contentType,
        string fileExtension,
        long byteLength,
        int width,
        int height,
        string? originalFileName,
        string ocrOutcome,
        string? ocrProviderVersion,
        decimal? ocrSuggestion,
        decimal? ocrConfidence)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        BillingCycleId = billingCycleId;
        BillingUnitBillId = billingUnitBillId;
        UnitId = unitId;
        UploadedByUserId = uploadedByUserId;
        StorageKey = storageKey;
        ContentType = contentType;
        FileExtension = fileExtension;
        ByteLength = byteLength;
        Width = width;
        Height = height;
        OriginalFileName = originalFileName;
        OcrOutcome = ocrOutcome;
        OcrProviderVersion = ocrProviderVersion;
        OcrSuggestion = ocrSuggestion;
        OcrConfidence = ocrConfidence;
        Status = MeterPhotoStatus.Staged;
        UploadedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid BillingCycleId { get; private set; }
    public Guid BillingUnitBillId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string FileExtension { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string? OriginalFileName { get; private set; }
    public string OcrOutcome { get; private set; } = string.Empty;
    public string? OcrProviderVersion { get; private set; }
    public decimal? OcrSuggestion { get; private set; }
    public decimal? OcrConfidence { get; private set; }
    public MeterPhotoStatus Status { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public DateTimeOffset? AttachedAtUtc { get; private set; }
    public decimal? ConfirmedReading { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }

    public void Attach(Guid actorUserId, decimal? confirmedReading)
    {
        Status = MeterPhotoStatus.Attached;
        AttachedAtUtc ??= DateTimeOffset.UtcNow;
        ConfirmedByUserId = actorUserId;
        ConfirmedReading = confirmedReading;
    }

    public void MarkOrphaned() => Status = MeterPhotoStatus.Orphaned;
}
