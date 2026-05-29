namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Main orchestrator interface for managing background jobs at runtime.
/// Provides high-level operations for configuration, execution history, and job status.
/// </summary>
public interface ISwitchboard
{
    /// <summary>
    /// Retrieves all job configurations from the repository.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of all job configurations.</returns>
    Task<IEnumerable<JobConfiguration>> GetJobConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single job configuration by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The job configuration if found; otherwise, null.</returns>
    Task<JobConfiguration?> GetJobConfigurationAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates a job configuration. Validates cron expression before persisting.
    /// </summary>
    /// <param name="configuration">The job configuration to persist.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the cron expression is invalid.</exception>
    Task UpdateJobConfigurationAsync(JobConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated execution history for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of job executions ordered by start time descending.</returns>
    Task<IEnumerable<JobExecution>> GetJobExecutionHistoryAsync(
        string jobId, 
        int pageSize, 
        int pageNumber, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the next scheduled execution time for a job based on its cron expression.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The next scheduled time if calculable; otherwise, null.</returns>
    Task<DateTimeOffset?> GetNextScheduledTimeAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers immediate execution of a job, bypassing the schedule.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TriggerJobNowAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current execution status of a job (running, last success/failure, etc.).
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The most recent job execution if found; otherwise, null.</returns>
    Task<JobExecution?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default);
}

