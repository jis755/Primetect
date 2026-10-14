namespace PrimeTect.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    BuildingStaff = 3,
    FinanceManager = 4,
}

public enum OrganizationPermission
{
    ViewAssignedProperties = 1,
    ViewMeterReadings = 2,
    EditMeterReadings = 3,
    SubmitMeterReadings = 4,
    ViewApprovedBills = 5,
    ReportCollections = 6,
    ReviewCollections = 7,
    RequestCollectionReversals = 8,
    ReviewCollectionReversals = 9,
    ManageTenants = 10,
    ManageProperties = 11,
    ManageCharges = 12,
    ReviewBills = 13,
    ReportRoomMeterIssues = 14,
    ReviewRoomMeterIssues = 15,
}

public enum PropertyType
{
    ApartmentBuilding = 1,
    IndividualRoomBuilding = 2,
    Shop = 3,
}

public enum OccupancyStatus
{
    Unknown = 1,
    Occupied = 2,
    Vacant = 3,
}

public enum ChargeType
{
    Electricity = 1,
    Water = 2,
    Gas = 3,
    ServiceCharge = 4,
    Rent = 5,
    Custom = 6,
}

public enum ChargeSettingMode
{
    Inherited = 1,
    Custom = 2,
    Disabled = 3,
}

public enum BillingCycleStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
}

public enum BillingAuditAction
{
    Generated = 1,
    Saved = 2,
    Submitted = 3,
    Returned = 4,
    Approved = 5,
}

public enum MeterReadingSource
{
    Missing = 1,
    ApprovedPeriod = 2,
    OpeningEntry = 3,
}

public enum CollectionReportStatus
{
    Pending = 1,
    Returned = 2,
    Approved = 3,
}

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    MobilePayment = 3,
    Other = 4,
}

public enum CollectionReversalStatus
{
    Pending = 1,
    Returned = 2,
    Approved = 3,
}

public enum CollectionAuditAction
{
    Submitted = 1,
    Corrected = 2,
    Returned = 3,
    Approved = 4,
    ReversalRequested = 5,
    ReversalReturned = 6,
    ReversalApproved = 7,
}

public enum RoomMeterIssueType
{
    DamagedMeter = 1,
    UnreadableMeter = 2,
    RoomMaintenance = 3,
    Other = 4,
}

public enum RoomMeterIssueTarget
{
    Unit = 1,
    BuildingCommonArea = 2,
}

public enum RoomMeterIssueStatus
{
    Open = 1,
    Acknowledged = 2,
    Resolved = 3,
}

public enum RoomMeterIssueAuditAction
{
    Reported = 1,
    Acknowledged = 2,
    Resolved = 3,
}

public enum MeterQrPlacement
{
    RoomDoor = 1,
    ElectricityMeter = 2,
}

public enum MeterPhotoStatus
{
    Staged = 1,
    Attached = 2,
    Orphaned = 3,
}

public enum OnboardingStatus
{
    NotConnected = 1,
}

public enum MeterInstallationAuditAction
{
    Installed = 1,
    Adopted = 2,
    Replaced = 3,
    OpeningBaseline = 4,
}
