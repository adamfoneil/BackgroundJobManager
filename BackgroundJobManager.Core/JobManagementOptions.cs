namespace BackgroundJobManager.Core;

/// <summary>
/// Configuration options for the job management system.
/// </summary>
public class JobManagementOptions
{
    /// <summary>
    /// Gets or sets the interval in seconds between polling cycles for checking job schedules.
    /// Default: 30 seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default number of days to retain job execution history.
    /// Jobs can override this with their own RetentionDays setting.
    /// Default: 30 days.
    /// </summary>
    public int DefaultHistoryRetentionDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum execution time in minutes before a job is considered timed out.
    /// Default: 60 minutes.
    /// </summary>
    public int JobExecutionTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the duration for which a distributed lock is held during job execution.
    /// Should be longer than typical job execution times to prevent double execution.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the unique identifier for this application instance.
    /// Used for distributed locking and execution tracking.
    /// Default: Machine name from Environment.MachineName.
    /// </summary>
    public string InstanceId { get; set; } = Environment.MachineName;
}
