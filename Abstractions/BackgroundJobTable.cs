using Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abstractions;

/// <summary>
/// entity class added to all db instances
/// </summary>
public class BackgroundJobItem    
{
    /// <summary>
    /// SwitchboardBackgroundService type
    /// </summary>
    public string ServiceType { get; set; } = default!;
    public ServiceStatus Status { get; set; }
    public int IntervalMinutes { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
    /// <summary>
    /// ExecuteResult.ResultType
    /// </summary>
    public ExecuteResultType? LastRunResult { get; set; }
    /// <summary>
    /// ExecuteResult.AllMessages as json array
    /// </summary>
    public string? LastRunMessages { get; set; }    
    public TimeSpan? LastRunDuration { get; set; }    
}

public class BackgrounJobItemSql : ISqlCreateTable
{
    public string IfNotExists(string tableName)
    {
        return $@"
        CREATE TABLE IF NOT EXISTS `{tableName}` (
            `ServiceType` VARCHAR(500) NOT NULL PRIMARY KEY,
            `Status` INT NOT NULL,
            `IntervalMinutes` INT NOT NULL,
            `NextRunUtc` DATETIME NULL,
            `LastRunUtc` DATETIME NULL,
            `LastRunResult` INT NULL,
            `LastRunMessages` TEXT NULL,
            `LastRunDuration` BIGINT NULL
        );";
    }
}

public class BackgrounJobConnector<TDbContext>(IDbContextFactory<TDbContext> dbFactory, ISqlCreateTable createTable) where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _dbFactory = dbFactory;
    private readonly ISqlCreateTable _createTable = createTable;

    public async Task EnsureTableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        using var db = _dbFactory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(_createTable.IfNotExists(tableName), cancellationToken);)
    }

    public static void ConfigureEntity(EntityTypeBuilder<BackgroundJobItem> builder, string tableName)
    {
        builder.ToTable(tableName);

        builder.HasKey(x => x.ServiceType);
        builder.Property(x => x.ServiceType).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.IntervalMinutes).IsRequired();
        builder.Property(x => x.NextRunUtc);
        builder.Property(x => x.LastRunUtc);
        builder.Property(x => x.LastRunResult);
        builder.Property(x => x.LastRunMessages);
        builder.Property(x => x.LastRunDuration);
    }
}