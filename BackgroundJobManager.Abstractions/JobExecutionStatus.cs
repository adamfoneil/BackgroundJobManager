namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Represents the execution status of a background job.
/// </summary>
public enum JobExecutionStatus
{
    /// <summary>
    /// Job execution is scheduled but not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Job execution is currently in progress.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Job execution completed successfully.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Job execution failed with an error.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Job execution was skipped (e.g., previous execution still running).
    /// </summary>
    Skipped = 4
}
