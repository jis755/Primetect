using PrimeTect.Domain.Enums;

namespace PrimeTect.Api.Contracts;

public sealed record OwnerRegistrationRequest(string OrganizationName, string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record InvitationRequest(string Email, UserRole Role);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);

public sealed record PropertySetupRequest(string Name, string Address, PropertyType PropertyType, IReadOnlyList<FloorDraft> Floors);

public sealed record PropertyUpdateRequest(string Name, string Address, PropertyType PropertyType, bool IsArchived);

public sealed record FloorDraft(string Name, int UnitCount, IReadOnlyList<string>? UnitNames);

public sealed record FloorUpdateRequest(string Name, int SortOrder, IReadOnlyList<string> UnitNames, bool IsArchived);

public sealed record UnitUpdateRequest(string Name, bool IsEnabled, bool IsArchived);

public sealed record OccupancyStatusRequest(OccupancyStatus Status);

public sealed record TenantCreateRequest(string? Name, string? Phone);

public sealed record TenantUpdateRequest(string? Name, string? Phone);

public sealed record OccupancyAssignmentRequest(Guid TenantId, DateOnly StartDate, DateOnly? EndDate);

public sealed record CloseOccupancyRequest(DateOnly EndDate);

public sealed record ChargeSettingRequest(ChargeType ChargeType, ChargeSettingMode Mode, DateOnly BillingMonth, decimal? Amount, string? CustomLabel);

public sealed record ChargeSettingsRequest(IReadOnlyList<ChargeSettingRequest> Items);

public sealed record ApiUserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

public sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

public sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, IReadOnlyList<UnitResponse> Units);

public sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, IReadOnlyList<FloorResponse> Floors);

public sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);

public sealed record ChargeConfigurationResponse(Guid Id, Guid? UnitId, ChargeType ChargeType, ChargeSettingMode Mode, DateOnly BillingMonth, decimal? Amount, string? CustomLabel);
