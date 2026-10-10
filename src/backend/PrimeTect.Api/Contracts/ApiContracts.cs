using PrimeTect.Domain.Enums;

namespace PrimeTect.Api.Contracts;

public sealed record OwnerRegistrationRequest(string OrganizationName, string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record InvitationRequest(string Email, UserRole Role);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);

public sealed record PropertySetupRequest(string Name, string Address, PropertyType PropertyType, IReadOnlyList<FloorDraft> Floors, string? TimeZoneId = null);

public sealed record PropertyUpdateRequest(string Name, string Address, PropertyType PropertyType, bool IsArchived, string? TimeZoneId = null);

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
    DateOnly? CurrentReadingDate = null);

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
    Guid UnitId,
    string UnitName,
    string? FloorName,
    Guid ReporterUserId,
    string ReporterName,
    RoomMeterIssueType IssueType,
    string Description,
    DateOnly ObservedDate,
    DateTimeOffset CreatedAtUtc,
    RoomMeterIssueStatus Status,
    long Version,
    string? ResolutionReason,
    IReadOnlyList<RoomMeterIssueAuditResponse> Audit);

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
    int OpenIssueCount);

public sealed record MeterOverviewResponse(
    Guid PropertyId,
    DateOnly BillingMonth,
    Guid? BillingCycleId,
    string? BillingCycleStatus,
    IReadOnlyList<MeterRoomOverviewResponse> Units);

public sealed record ApiUserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

public sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

public sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, IReadOnlyList<UnitResponse> Units);

public sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, IReadOnlyList<FloorResponse> Floors, string TimeZoneId);

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
    decimal Amount);

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
    decimal Amount);

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
