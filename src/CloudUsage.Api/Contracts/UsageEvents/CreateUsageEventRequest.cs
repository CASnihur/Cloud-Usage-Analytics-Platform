using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record CreateUsageEventRequest(
    Guid EventId,

    [property: Required]
    [property: StringLength(128)]
    string UserId,

    [property: Required]
    [property: StringLength(64)]
    string ProductCode,

    [property: Required]
    [property: StringLength(64)]
    string EventType,

    DateTimeOffset OccurredAtUtc,
    JsonElement? Properties);

