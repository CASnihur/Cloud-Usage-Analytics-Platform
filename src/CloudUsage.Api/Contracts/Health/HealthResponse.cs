namespace CloudUsage.Api.Contracts.Health;

public sealed record HealthResponse(string Status, DateTimeOffset CheckedAtUtc);
