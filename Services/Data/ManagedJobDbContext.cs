using Microsoft.EntityFrameworkCore;
using ManagedBackgroundJob.Abstractions.Entities;

namespace ManagedBackgroundJob.Abstractions.Data;

/// <summary>
/// DbContext for managing background job state
/// </summary>
public class ManagedJobDbContext : DbContext
{
    public ManagedJobDbContext(DbContextOptions<ManagedJobDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobConfiguration> JobConfigurations { get; set; }
    public DbSet<JobRun> JobRuns { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JobConfiguration entity configuration
        modelBuilder.Entity<JobConfiguration>(entity =>
        {
            entity.HasKey(e => e.ServiceName);
            entity.Property(e => e.ServiceName).HasMaxLength(200);
            entity.Property(e => e.TimeZoneId).HasMaxLength(100);
            entity.Property(e => e.CronSchedule).HasMaxLength(100);

            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.NextScheduledRun);
        });

        // JobRun entity configuration
        modelBuilder.Entity<JobRun>(entity =>
        {
            entity.HasKey(e => e.RunId);
            entity.Property(e => e.RunId).HasMaxLength(50);
            entity.Property(e => e.ServiceName).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(2000);

            entity.HasIndex(e => e.ServiceName);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => new { e.ServiceName, e.StartedAt });
            entity.HasIndex(e => e.FinishedAt);
        });
    }
}
