namespace ManagedBackgroundJob.Abstractions.Entities;

/// <summary>
/// Represents the configuration for a managed background job
/// </summary>
public class JobConfiguration
{
    /// <summary>
    /// Unique identifier for the job (typically the service name)
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the job is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Cron expression for scheduling (e.g., "0 */5 * * * *" for every 5 minutes)
    /// If null or empty, the job runs continuously
    /// </summary>
    public string? CronSchedule { get; set; }

    /// <summary>
    /// Timezone for schedule evaluation (defaults to UTC)
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Last time the schedule was evaluated (for continuous scheduling)
    /// </summary>
    public DateTimeOffset? LastScheduleCheck { get; set; }

    /// <summary>
    /// Next scheduled run time (calculated from cron expression)
    /// </summary>
    public DateTimeOffset? NextScheduledRun { get; set; }
}
