using CloudUsage.Api.Application.UsageEvents;
using CloudUsage.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

namespace CloudUsage.Api.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLOUD_USAGE_SQL_TESTS") != "1")
            Skip = "Set CLOUD_USAGE_SQL_TESTS=1 with local API User Secrets configured to run SQL Server tests.";
    }
}

public sealed class UsageEventSqlTests
{
    // Both requests reach SaveChanges only after their pre-check has returned false.
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) 
                ready.SetResult();

            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

            return result;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-09-05T12:00:00Z");
    }

    [SqlServerFact]
    public async Task Service_PersistsDetectsDuplicatesAndHandlesConcurrentInsert()
    {
        var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("UsageAnalyticsDatabase")
            ?? throw new InvalidOperationException("Configure the API local database User Secret.");

        var databaseName = "CloudUsageTests_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = databaseName, ConnectTimeout = 5 };

        DbContextOptions<UsageAnalyticsDbContext> Options(SaveBarrier? barrier = null)
        {
            var options = new DbContextOptionsBuilder<UsageAnalyticsDbContext>().UseSqlServer(builder.ConnectionString);
            if (barrier is not null) 
                options.AddInterceptors(barrier);

            return options.Options;
        }

        UsageEventIngestionService Service(UsageAnalyticsDbContext db) =>
            new(db, new FixedClock(), NullLogger<UsageEventIngestionService>.Instance);

        var command = new IngestUsageEventCommand(Guid.NewGuid(), "test-user", "studio", "feature_used",
            DateTimeOffset.Parse("2026-09-05T13:00:00+03:00"), "{\"feature\":\"test\"}");

        await using var db = new UsageAnalyticsDbContext(Options());
        try
        {
            await db.Database.MigrateAsync();

            var created = Assert.IsType<UsageEventIngestionResult.Created>(await Service(db).IngestAsync(command, default));
            Assert.True(created.RawEventId > 0);
            Assert.Equal(new FixedClock().GetUtcNow(), created.ReceivedAtUtc);

            db.ChangeTracker.Clear();
            var stored = await db.RawUsageEvents.SingleAsync();
            Assert.Equal(command.PropertiesJson, stored.PropertiesJson);
            Assert.Equal(TimeSpan.Zero, stored.OccurredAtUtc.Offset);
            Assert.Equal(command.OccurredAtUtc, stored.OccurredAtUtc);
            Assert.IsType<UsageEventIngestionResult.Duplicate>(await Service(db).IngestAsync(command, default));

            var barrier = new SaveBarrier();
            await using var first = new UsageAnalyticsDbContext(Options(barrier));
            await using var second = new UsageAnalyticsDbContext(Options(barrier));

            var concurrent = command with { EventId = Guid.NewGuid() };
            var results = await Task.WhenAll(
                Service(first).IngestAsync(concurrent, default),
                Service(second).IngestAsync(concurrent, default));

            Assert.Single(results.OfType<UsageEventIngestionResult.Created>());
            Assert.Single(results.OfType<UsageEventIngestionResult.Duplicate>());
            Assert.Equal(1, await db.RawUsageEvents.CountAsync(e => e.EventId == concurrent.EventId));
            Assert.DoesNotContain(first.ChangeTracker.Entries(), e => e.State == EntityState.Added);
            Assert.DoesNotContain(second.ChangeTracker.Entries(), e => e.State == EntityState.Added);

            // Exercise the actual controller, scoped service registration, and SQL provider together.
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Testing");
                host.UseSetting("ConnectionStrings:UsageAnalyticsDatabase", builder.ConnectionString);
                host.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
            });

            using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

            var httpEventId = Guid.NewGuid();
            var payload = new { 
                eventId = httpEventId, 
                userId = "http-user", 
                productCode = "studio",
                eventType = "feature_used", 
                occurredAtUtc = "2026-09-05T10:00:00Z" };

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/usage-events", payload)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/usage-events", payload)).StatusCode);
            Assert.Equal(1, await db.RawUsageEvents.CountAsync(e => e.EventId == httpEventId));

            await Assert.ThrowsAsync<DbUpdateException>(() => Service(db).IngestAsync(
                command with { EventId = Guid.NewGuid(), PropertiesJson = "invalid-json" }, default));

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db).IngestAsync(command, cancelled.Token));
        }
        finally
        {
            // This context points exclusively to this test's randomly named database.
            await db.Database.EnsureDeletedAsync();
        }
    }
}
