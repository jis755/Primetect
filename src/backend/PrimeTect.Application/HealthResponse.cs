namespace PrimeTect.Application;

public sealed record HealthResponse(string Service, string Status, DateTimeOffset UtcTime);
