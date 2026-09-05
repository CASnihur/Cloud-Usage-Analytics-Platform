namespace CloudUsage.Api.Application.UsageEvents;

public interface IUsageEventIngestionService
{
    Task<UsageEventIngestionResult> IngestAsync(
        IngestUsageEventCommand command,
        CancellationToken cancellationToken);
}
