namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record UsageEventBatchItemResult(
    int Index,
    int Status,
    CreateUsageEventResponse? Event,
    Dictionary<string, string[]>? Errors,
    string? Detail);
