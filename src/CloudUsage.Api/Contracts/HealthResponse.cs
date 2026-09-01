namespace CloudUsage.Api.Contracts;

public sealed record HealthResponse(string Status, DateTimeOffset CheckedAtUtc);
