using PrimeTect.Domain.Enums;

namespace PrimeTect.Api.Contracts;

public sealed record OwnerRegistrationRequest(string OrganizationName, string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record InvitationRequest(string Email, UserRole Role);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);

public sealed record PropertySetupRequest(string Name, string Address, PropertyType PropertyType, IReadOnlyList<FloorDraft> Floors, string? TimeZoneId = null, decimal? Latitude = null, decimal? Longitude = null);

public sealed record PropertyUpdateRequest(string Name, string Address, PropertyType PropertyType, bool IsArchived, string? TimeZoneId = null, decimal? Latitude = null, decimal? Longitude = null);

public sealed record FloorDraft(string Name, int UnitCount, IReadOnlyList<string>? UnitNames);

public sealed record FloorUpdateRequest(string Name, int SortOrder, IReadOnlyList<string> UnitNames, bool IsArchived);

public sealed record UnitUpdateRequest(string Name, bool IsEnabled, bool IsArchived);

public sealed record OccupancyStatusRequest(OccupancyStatus Status);

public sealed record TenantCreateRequest(string? Name, string? Phone);

public sealed record TenantUpdateRequest(string? Name, string? Phone);

public sealed record OccupancyAssignmentRequest(Guid TenantId, DateOnly StartDate, DateOnly? EndDate);

public sealed record CloseOccupancyRequest(DateOnly EndDate);

public sealed record ChargeSettingRequest(
    ChargeType ChargeType,
    ChargeSettingMode Mode,
    DateOnly BillingMonth,
    decimal? Amount,
    string? CustomLabel,
    ChargeSettingMode? WarningMode = null,
    decimal? WarningThreshold = null);

public sealed record ChargeSettingsRequest(IReadOnlyList<ChargeSettingRequest> Items);

public sealed record BillingMonthRequest(DateOnly BillingMonth);

public sealed record MeterReadingEntryRequest(
    Guid UnitBillId,
    decimal? PreviousReading,
    decimal? CurrentReading,
    DateOnly? PreviousReadingDate = null,
    DateOnly? CurrentReadingDate = null,
    Guid? MeterPhotoId = null);

public sealed record EndChargeRequest(DateOnly BillingMonth);

public sealed record BillingReadingsRequest(long ExpectedVersion, IReadOnlyList<MeterReadingEntryRequest> Entries, string? Reason);

public sealed record BillingActionRequest(long ExpectedVersion, string? Reason);

public sealed record RolePermissionUpdateRequest(UserRole Role, IReadOnlyDictionary<string, bool> Permissions, string? Reason);

public sealed record RolePermissionConfigurationResponse(UserRole Role, IReadOnlyDictionary<string, bool> Permissions);

public sealed record CollectionReportCreateRequest(
    Guid BillingUnitBillId,
    decimal AmountReceived,
    DateOnly ReceivedDate,
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    Guid SubmissionKey);

public sealed record CollectionReportCorrectionRequest(
    long ExpectedVersion,
    decimal AmountReceived,
    DateOnly ReceivedDate,
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    string? Reason);

public sealed record CollectionActionRequest(long ExpectedVersion, string? Reason);

public sealed record CollectionReportResponse(
    Guid Id,
    Guid BillingUnitBillId,
    Guid BillingCycleId,
    Guid PropertyId,
    DateOnly BillingMonth,
    Guid UnitId,
    string UnitName,
    string? FloorName,
    Guid? OccupancyAssignmentId,
    Guid? TenantId,
    string? TenantName,
    string? TenantPhone,
    decimal BilledAmount,
    decimal ConfirmedCollectedAmount,
    decimal RemainingBalance,
    decimal AmountReceived,
    DateOnly ReceivedDate,
    PaymentMethod PaymentMethod,
    string? ReferenceNote,
    Guid ReporterUserId,
    string ReporterName,
    DateTimeOffset SubmittedAtUtc,
    CollectionReportStatus Status,
    long Version,
    string? ReturnReason,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAtUtc,
    bool IsReversed,
    CollectionReversalStatus? ReversalStatus,
    Guid? ReversalId,
    long? ReversalVersion,
    string? ReversalReason,
    IReadOnlyList<CollectionAuditResponse> Audit);

public sealed record CollectionAuditResponse(
    Guid Id,
    Guid? CollectionReversalId,
    Guid ActorUserId,
    CollectionAuditAction Action,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    string? Before,
    string? After);

public sealed record CollectionQueueResponse(
    IReadOnlyList<CollectionReportResponse> Items,
    decimal BilledTotal,
    decimal PendingReportedTotal,
    decimal ConfirmedCollectedTotal,
    decimal OutstandingTotal);

public sealed record CollectionUnitOverviewResponse(
    Guid UnitId,
    string UnitName,
    string? FloorName,
    Guid? BillingUnitBillId,
    Guid? OccupancyAssignmentId,
    Guid? TenantId,
    string? TenantName,
    decimal? BilledAmount,
    decimal ConfirmedCollectedAmount,
    decimal PendingReportedAmount,
    decimal PendingReversalAmount,
    decimal? RemainingBalance,
    string Status);

public sealed record CollectionOverviewResponse(
    Guid PropertyId,
    DateOnly BillingMonth,
    Guid? BillingCycleId,
    string BillingCoverage,
    IReadOnlyList<CollectionUnitOverviewResponse> Units,
    decimal BilledTotal,
    decimal PendingReportedTotal,
    decimal ConfirmedCollectedTotal,
    decimal OutstandingTotal,
    decimal PendingReversalTotal);

public sealed record RoomMeterIssueCreateRequest(
    RoomMeterIssueType IssueType,
    string Description,
    DateOnly ObservedDate);

public sealed record RoomMeterIssueActionRequest(long ExpectedVersion, string? Reason);

public sealed record RoomMeterIssueAuditResponse(
    Guid Id,
    Guid ActorUserId,
    RoomMeterIssueAuditAction Action,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    string? Before,
    string? After);

public sealed record RoomMeterIssueResponse(
    Guid Id,
    Guid PropertyId,
    Guid? UnitId,
    string UnitName,
    string? FloorName,
    Guid ReporterUserId,
    string ReporterName,
    RoomMeterIssueType? IssueType,
    string Description,
    DateOnly? ObservedDate,
    DateTimeOffset CreatedAtUtc,
    RoomMeterIssueStatus Status,
    long Version,
    string? ResolutionReason,
    IReadOnlyList<RoomMeterIssueAuditResponse> Audit,
    RoomMeterIssueTarget Target,
    bool IsFeedback);

public sealed record FeedbackReportCreateRequest(Guid? UnitId, string Note, Guid SubmissionKey);

public sealed record NotificationResponse(
    Guid Id,
    Guid RoomMeterIssueId,
    Guid PropertyId,
    Guid? UnitId,
    string Title,
    string PropertyName,
    string UnitLabel,
    string TimeZoneId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead);

public sealed record MeterQrCreateRequest(MeterQrPlacement Placement);

public sealed record MeterQrResponse(
    Guid Id,
    Guid PropertyId,
    Guid UnitId,
    string PropertyName,
    string UnitName,
    MeterQrPlacement Placement,
    string Url,
    string Svg,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    bool IsActive);

public sealed record MeterQrResolveResponse(
    Guid PropertyId,
    Guid UnitId,
    string PropertyName,
    string UnitName,
    string BillingMonth,
    bool IsActive);

public sealed record MeterPhotoResponse(
    Guid Id,
    Guid PropertyId,
    Guid UnitId,
    Guid BillingCycleId,
    Guid BillingUnitBillId,
    string ContentType,
    string FileExtension,
    long ByteLength,
    int Width,
    int Height,
    string? OriginalFileName,
    string OcrOutcome,
    string? OcrProviderVersion,
    decimal? OcrSuggestion,
    decimal? OcrConfidence,
    string? OcrExplanation,
    MeterPhotoStatus Status,
    bool IsAttached,
    string PhotoUrl,
    Guid? MeterInstallationId = null);

public sealed record MeterRoomOverviewResponse(
    Guid UnitId,
    string UnitName,
    string? FloorName,
    bool ElectricityApplicable,
    decimal? PreviousReading,
    DateOnly? PreviousReadingDate,
    decimal? CurrentReading,
    DateOnly? CurrentReadingDate,
    decimal? ConsumptionUnits,
    decimal? ElectricityAmount,
    decimal? ElectricityRate,
    string ReadingStatus,
    string? BlockingReason,
    int ReportedIssueCount,
    int OpenIssueCount,
    Guid? MeterInstallationId = null,
    string? SubmeterLabel = null,
    bool? SubmeterIdentityKnown = null,
    bool? SubmeterFinalReadingUnavailable = null);

public sealed record MeterOverviewResponse(
    Guid PropertyId,
    DateOnly BillingMonth,
    Guid? BillingCycleId,
    string? BillingCycleStatus,
    IReadOnlyList<MeterRoomOverviewResponse> Units);

public sealed record ApiUserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

public sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

public sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, IReadOnlyList<UnitResponse> Units);

public sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, IReadOnlyList<FloorResponse> Floors, string TimeZoneId, decimal? Latitude, decimal? Longitude);

public sealed record TenantCurrentUnitResponse(Guid PropertyId, string PropertyName, Guid UnitId, string UnitName, DateOnly StartDate);

public sealed record TenantResponse(
    Guid Id,
    string? Name,
    string? Phone,
    OnboardingStatus OnboardingStatus,
    IReadOnlyList<TenantCurrentUnitResponse>? CurrentAssignments = null);

public sealed record ChargeConfigurationResponse(
    Guid Id,
    Guid? UnitId,
    ChargeType ChargeType,
    ChargeSettingMode Mode,
    DateOnly BillingMonth,
    decimal? Amount,
    string? CustomLabel,
    ChargeSettingMode WarningMode,
    decimal? WarningThreshold);

public sealed record BillingChargeLineResponse(
    Guid Id,
    ChargeType ChargeType,
    string ChargeName,
    ChargeSettingMode SettingMode,
    bool IsMetered,
    decimal? Rate,
    decimal? PreviousReading,
    DateOnly? PreviousReadingDate,
    decimal? CurrentReading,
    DateOnly? CurrentReadingDate,
    decimal? ConsumptionUnits,
    MeterReadingSource PreviousReadingSource,
    bool WarningEnabled,
    decimal? WarningThreshold,
    decimal Amount,
    Guid? MeterPhotoId,
    Guid? MeterInstallationId = null,
    IReadOnlyList<BillingMeterSegmentResponse>? Segments = null,
    string? MeterLabel = null);

public sealed record BillingUnitBillResponse(
    Guid Id,
    Guid UnitId,
    string UnitName,
    Guid? OccupancyAssignmentId,
    Guid? TenantId,
    string? TenantName,
    string? TenantPhone,
    DateOnly? OccupancyStartDate,
    DateOnly? OccupancyEndDate,
    string? BlockingReason,
    decimal Total,
    bool ElectricityWarning,
    IReadOnlyList<BillingChargeLineResponse> Charges);

public sealed record BillingAuditResponse(
    Guid Id,
    Guid ActorUserId,
    BillingAuditAction Action,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    string? Before,
    string? After);

public sealed record BillingCycleResponse(
    Guid Id,
    Guid PropertyId,
    DateOnly BillingMonth,
    BillingCycleStatus Status,
    long Version,
    Guid LastChangedByUserId,
    DateTimeOffset LastChangedAtUtc,
    string? ReturnReason,
    decimal DraftTotal,
    decimal? ApprovedTotal,
    int BlockingBillCount,
    IReadOnlyList<BillingUnitBillResponse> Bills,
    IReadOnlyList<BillingAuditResponse> Audit);

public sealed record BillingStatementChargeResponse(
    Guid Id,
    ChargeType ChargeType,
    string ChargeName,
    ChargeSettingMode SettingMode,
    bool IsMetered,
    decimal? Rate,
    decimal? PreviousReading,
    DateOnly? PreviousReadingDate,
    decimal? CurrentReading,
    DateOnly? CurrentReadingDate,
    decimal? ConsumptionUnits,
    bool WarningEnabled,
    decimal? WarningThreshold,
    decimal Amount,
    Guid? MeterPhotoId,
    Guid? MeterInstallationId = null,
    IReadOnlyList<BillingMeterSegmentResponse>? Segments = null,
    string? MeterLabel = null);

public sealed record BillingStatementUnitResponse(
    string BillReference,
    Guid BillId,
    Guid UnitId,
    string UnitName,
    string? FloorName,
    Guid? OccupancyAssignmentId,
    Guid? TenantId,
    string? TenantName,
    string? TenantPhone,
    DateOnly? OccupancyStartDate,
    DateOnly? OccupancyEndDate,
    decimal Total,
    IReadOnlyList<BillingStatementChargeResponse> Charges);

public sealed record BillingStatementResponse(
    string StatementType,
    Guid PropertyId,
    Guid BillingCycleId,
    string PropertyName,
    string PropertyAddress,
    DateOnly BillingMonth,
    DateTimeOffset ApprovalDate,
    decimal ApprovedTotal,
    Guid? TenantId,
    IReadOnlyList<BillingStatementUnitResponse> Bills);

public sealed record BillingFloorSummaryResponse(
    string FloorName,
    decimal TotalBilled,
    IReadOnlyList<BillingStatementUnitResponse> Bills);

public sealed record BillingPropertySummaryResponse(
    Guid PropertyId,
    Guid BillingCycleId,
    string PropertyName,
    string PropertyAddress,
    DateOnly BillingMonth,
    DateTimeOffset ApprovalDate,
    decimal ApprovedTotal,
    IReadOnlyList<BillingFloorSummaryResponse> Floors);

public sealed record BillingComparisonMetricResponse(
    string Key,
    ChargeType? ChargeType,
    string Label,
    decimal? CurrentAmount,
    decimal? PreviousAmount,
    decimal? Difference,
    decimal? PercentageChange,
    bool PercentageChangeAvailable,
    int CurrentUnitCount,
    int PreviousUnitCount,
    bool CoverageChanged);

public sealed record BillingComparisonConsumptionResponse(
    decimal? CurrentUnits,
    decimal? PreviousUnits,
    decimal? Difference,
    decimal? PercentageChange,
    bool PercentageChangeAvailable,
    int CurrentUnitCount,
    int PreviousUnitCount,
    bool CoverageChanged);

public sealed record BillingComparisonResponse(
    Guid PropertyId,
    string PropertyName,
    string PropertyAddress,
    DateOnly BillingMonth,
    Guid BillingCycleId,
    decimal CurrentApprovedTotal,
    decimal? PreviousApprovedTotal,
    decimal? TotalDifference,
    decimal? TotalPercentageChange,
    bool TotalPercentageChangeAvailable,
    DateOnly PreviousMonth,
    string PreviousPeriodStatus,
    bool IsComplete,
    string? IncompleteReason,
    BillingComparisonConsumptionResponse Electricity,
    IReadOnlyList<BillingComparisonMetricResponse> Charges,
    IReadOnlyList<string> CoverageNotes);

public sealed record BillingMeterSegmentResponse(
    Guid? MeterInstallationId,
    int SortOrder,
    DateOnly? SegmentStartDate,
    DateOnly? SegmentEndDate,
    decimal? PreviousReading,
    MeterReadingSource PreviousReadingSource,
    DateOnly? PreviousReadingDate,
    decimal? CurrentReading,
    DateOnly? CurrentReadingDate,
    decimal? ConsumptionUnits,
    bool FinalReadingUnavailable);

public sealed record UtilityAccountRequest(string ProviderName, string CustomerNumber, string? DisplayLabel);

public sealed record UtilityAccountResponse(
    Guid Id,
    Guid PropertyId,
    string ProviderName,
    string CustomerNumber,
    string? DisplayLabel,
    DateTimeOffset CreatedAtUtc);

public sealed record UtilityMainMeterRequest(Guid UtilityAccountId, string MeterNumber, string? DisplayLabel, DateOnly? InstalledOn);

public sealed record UtilityMainMeterEndRequest(DateOnly EndedOn);

public sealed record UtilityMainMeterResponse(
    Guid Id,
    Guid PropertyId,
    Guid UtilityAccountId,
    string MeterNumber,
    string? DisplayLabel,
    DateOnly? InstalledOn,
    DateOnly? EndedOn,
    bool IsActive);

public sealed record MeterInstallationRequest(
    Guid UnitId,
    Guid? MainMeterId,
    string? SerialNumber,
    string? DisplayLabel,
    DateOnly? InstallationDate,
    decimal? OpeningReading,
    DateOnly? OpeningReadingDate,
    bool? IdentityKnown,
    string? Reason);

public sealed record MeterInstallationUpdateRequest(
    Guid? MainMeterId,
    string? SerialNumber,
    string? DisplayLabel,
    DateOnly? InstallationDate,
    bool? IdentityKnown,
    string? Reason);

public sealed record MeterInstallationResponse(
    Guid Id,
    Guid PropertyId,
    Guid UnitId,
    string UnitName,
    Guid? MainMeterId,
    string? SerialNumber,
    string? DisplayLabel,
    DateOnly? InstallationDate,
    decimal? OpeningReading,
    DateOnly? OpeningReadingDate,
    Guid? ReplacedInstallationId,
    bool IdentityKnown,
    DateOnly? EndedOn,
    string? EndReason,
    decimal? FinalReading,
    bool FinalReadingUnavailable,
    bool IsActive,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record MeterReplacementRequest(
    long ExpectedInstallationVersion,
    DateTimeOffset ReplacementOccurredAtUtc,
    string Reason,
    decimal? OldFinalReading,
    bool OldFinalReadingUnavailable,
    string? NewSerialNumber,
    string? NewDisplayLabel,
    decimal NewOpeningReading,
    Guid? NewMainMeterId,
    Guid SubmissionKey);

public sealed record MeterReplacementResponse(
    Guid PreviousInstallationId,
    MeterInstallationResponse NewInstallation,
    DateTimeOffset ReplacementOccurredAtUtc,
    string PropertyLocalDate,
    bool AffectedDraftRecalculated,
    string? BlockingMessage);

public sealed record MeterInstallationAuditResponse(
    Guid Id,
    Guid InstallationId,
    Guid? PreviousInstallationId,
    Guid ActorUserId,
    MeterInstallationAuditAction Action,
    DateOnly? ReplacementDate,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    string? Before,
    string? After);

public sealed record MeterSetupResponse(
    Guid PropertyId,
    IReadOnlyList<UtilityAccountResponse> Accounts,
    IReadOnlyList<UtilityMainMeterResponse> MainMeters,
    IReadOnlyList<MeterInstallationResponse> Installations,
    IReadOnlyList<MeterInstallationAuditResponse> Audit);
