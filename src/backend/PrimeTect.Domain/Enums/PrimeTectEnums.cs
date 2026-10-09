namespace PrimeTect.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    BuildingStaff = 3,
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
}

public enum OnboardingStatus
{
    NotConnected = 1,
}
