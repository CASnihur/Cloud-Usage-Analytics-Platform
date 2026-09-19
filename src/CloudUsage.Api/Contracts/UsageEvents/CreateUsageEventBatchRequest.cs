using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record CreateUsageEventBatchRequest(
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    JsonElement[]? Events);
