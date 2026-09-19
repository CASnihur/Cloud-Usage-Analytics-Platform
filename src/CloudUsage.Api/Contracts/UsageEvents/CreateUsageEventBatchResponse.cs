namespace CloudUsage.Api.Contracts.UsageEvents;

public sealed record CreateUsageEventBatchResponse(
    IReadOnlyList<UsageEventBatchItemResult> Results);
