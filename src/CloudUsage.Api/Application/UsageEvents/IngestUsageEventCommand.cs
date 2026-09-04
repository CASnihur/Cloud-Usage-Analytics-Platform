namespace CloudUsage.Api.Application.UsageEvents;

public sealed record IngestUsageEventCommand(
    Guid EventId,
    string UserExternalId,
    string ProductCode,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string? PropertiesJson);
