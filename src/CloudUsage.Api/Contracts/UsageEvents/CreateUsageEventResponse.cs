namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record CreateUsageEventResponse(long RawEventId, Guid EventId, string IngestionStatus, DateTimeOffset ReceivedAtUtc);
