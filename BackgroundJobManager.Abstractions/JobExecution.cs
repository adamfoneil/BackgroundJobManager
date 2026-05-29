namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Represents a single execution record of a background job.
/// </summary>
public class JobExecution
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the job configuration ID this execution belongs to.
    /// </summary>
    public required string JobConfigurationId { get; set; }

    /// <summary>
    /// Gets or sets the time when the job was scheduled to run.
    /// </summary>
    public required DateTimeOffset ScheduledTime { get; set; }

    /// <summary>
    /// Gets or sets the actual start time of the job execution.
    /// </summary>
    public required DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the completion time of the job execution. Null if still running.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Pending;

    /// <summary>
    /// Gets or sets the error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the full stack trace if the execution failed.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the instance that executed this job (e.g., hostname).
    /// </summary>
    public string? ExecutedByInstance { get; set; }

    /// <summary>
    /// Gets or sets the duration of the execution in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Calculates and sets the duration in milliseconds from StartTime to EndTime.
    /// </summary>
    public void CalculateDuration()
    {
        if (EndTime.HasValue)
        {
            DurationMs = (long)(EndTime.Value - StartTime).TotalMilliseconds;
        }
    }
}
