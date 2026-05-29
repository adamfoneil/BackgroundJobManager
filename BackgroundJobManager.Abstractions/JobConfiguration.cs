namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Represents the configuration of a background job including its schedule and runtime settings.
/// </summary>
public class JobConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for the job.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified type name of the IJob implementation.
    /// </summary>
    public required string JobTypeName { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name for the job.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the cron expression defining when the job should execute.
    /// </summary>
    public required string CronSchedule { get; set; }

    /// <summary>
    /// Gets or sets whether the job is enabled and should be executed on schedule.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the timezone ID for cron schedule evaluation (default: UTC).
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Gets or sets the number of days to retain execution history for this job.
    /// If null, the global default retention policy applies.
    /// </summary>
    public int? RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for extensibility.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
