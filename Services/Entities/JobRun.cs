namespace ManagedBackgroundJob.Abstractions.Entities;

/// <summary>
/// Represents a single execution run of a background job
/// </summary>
public class JobRun
{
    /// <summary>
    /// Unique identifier for this run
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the service that executed
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// When the run started
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When the run finished (null if still running)
    /// </summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Whether the run succeeded
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Result message from the execution
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Exception details if the run failed
    /// </summary>
    public string? ExceptionDetails { get; set; }
}
