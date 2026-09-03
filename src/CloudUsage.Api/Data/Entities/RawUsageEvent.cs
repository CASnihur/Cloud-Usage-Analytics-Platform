namespace CloudUsage.Api.Data.Entities;

public sealed class RawUsageEvent
{
    private RawUsageEvent()
    {
    }

    public RawUsageEvent(
        Guid eventId,
        string userExternalId,
        string productCode,
        string eventType,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset receivedAtUtc,
        string? propertiesJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userExternalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        EventId = eventId;
        UserExternalId = userExternalId;
        ProductCode = productCode;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        ReceivedAtUtc = receivedAtUtc;
        PropertiesJson = propertiesJson;
        IngestionStatus = RawEventIngestionStatus.Pending;
    }

    public long RawEventId { get; private set; }

    public Guid EventId { get; private set; }

    public string UserExternalId { get; private set; } = null!;

    public string ProductCode { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public string? PropertiesJson { get; private set; }

    public RawEventIngestionStatus IngestionStatus { get; private set; }
}
