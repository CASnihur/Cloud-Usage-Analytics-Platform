using CloudUsage.Api.Data;
using CloudUsage.Api.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CloudUsage.Api.Application.UsageEvents;

public sealed class UsageEventIngestionService(
    UsageAnalyticsDbContext db,
    TimeProvider clock,
    ILogger<UsageEventIngestionService> logger) : IUsageEventIngestionService
{
    public async Task<UsageEventIngestionResult> IngestAsync(
        IngestUsageEventCommand command, CancellationToken cancellationToken)
    {
        if (await db.RawUsageEvents.AnyAsync(e => e.EventId == command.EventId, cancellationToken))
            return new UsageEventIngestionResult.Duplicate(command.EventId);

        var entity = new RawUsageEvent(command.EventId, command.UserExternalId,
            command.ProductCode, command.EventType, command.OccurredAtUtc.ToUniversalTime(),
            clock.GetUtcNow(), command.PropertiesJson);
        db.RawUsageEvents.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A failed insert stays Added in the tracker. Remove it before reusing this context.
            db.Entry(entity).State = EntityState.Detached;
            // Confirm the conflict is this event ID rather than masking another unique constraint.
            if (!await db.RawUsageEvents.AnyAsync(e => e.EventId == command.EventId, cancellationToken))
                throw;
            logger.LogInformation("Concurrent duplicate usage event {EventId}", command.EventId);
            return new UsageEventIngestionResult.Duplicate(command.EventId);
        }

        logger.LogInformation("Stored usage event {EventId} as {RawEventId}", entity.EventId, entity.RawEventId);
        return new UsageEventIngestionResult.Created(entity.RawEventId, entity.EventId,
            entity.IngestionStatus, entity.ReceivedAtUtc);
    }
}
