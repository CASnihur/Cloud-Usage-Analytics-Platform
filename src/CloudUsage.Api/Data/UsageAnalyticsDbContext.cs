using CloudUsage.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudUsage.Api.Data;

public sealed class UsageAnalyticsDbContext(DbContextOptions<UsageAnalyticsDbContext> options)
    : DbContext(options)
{
    public DbSet<RawUsageEvent> RawUsageEvents => Set<RawUsageEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsageAnalyticsDbContext).Assembly);
    }
}
