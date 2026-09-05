using CloudUsage.Api.Data.Entities;

namespace CloudUsage.Api.Application.UsageEvents;

public abstract record UsageEventIngestionResult
{
    public sealed record Created(
        long RawEventId,
        Guid EventId,
        RawEventIngestionStatus IngestionStatus,
        DateTimeOffset ReceivedAtUtc) : UsageEventIngestionResult;

    public sealed record Duplicate(Guid EventId) : UsageEventIngestionResult;
}
