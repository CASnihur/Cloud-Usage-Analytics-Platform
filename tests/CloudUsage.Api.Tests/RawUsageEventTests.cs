using CloudUsage.Api.Data.Entities;

namespace CloudUsage.Api.Tests;

public sealed class RawUsageEventTests
{
    [Fact]
    public void Constructor_CreatesPendingEventWithProvidedValues()
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2026, 9, 3, 14, 20, 15, TimeSpan.Zero);
        var receivedAtUtc = occurredAtUtc.AddSeconds(2);

        var usageEvent = new RawUsageEvent(
            eventId,
            "user-1042",
            "studio",
            "feature_used",
            occurredAtUtc,
            receivedAtUtc,
            "{\"featureName\":\"Workflow Analyzer\"}");

        Assert.Equal(eventId, usageEvent.EventId);
        Assert.Equal("user-1042", usageEvent.UserExternalId);
        Assert.Equal(occurredAtUtc, usageEvent.OccurredAtUtc);
        Assert.Equal(RawEventIngestionStatus.Pending, usageEvent.IngestionStatus);
    }

    [Fact]
    public void Constructor_RejectsBlankUserIdentifier()
    {
        var action = () => new RawUsageEvent(
            Guid.NewGuid(),
            " ",
            "studio",
            "feature_used",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        Assert.Throws<ArgumentException>(action);
    }
}
