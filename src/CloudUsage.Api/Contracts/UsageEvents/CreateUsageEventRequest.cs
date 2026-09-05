using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record CreateUsageEventRequest(
    Guid EventId,

    [Required]
    [StringLength(128)]
    string UserId,

    [Required]
    [StringLength(64)]
    string ProductCode,

    [Required]
    [StringLength(64)]
    string EventType,

    [Required]
    DateTimeOffset? OccurredAtUtc,
    JsonElement? Properties);

