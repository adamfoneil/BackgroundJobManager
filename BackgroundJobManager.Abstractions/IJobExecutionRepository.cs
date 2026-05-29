namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Repository interface for persisting and querying job execution history.
/// </summary>
public interface IJobExecutionRepository
{
    /// <summary>
    /// Creates a new job execution record in the repository.
    /// </summary>
    /// <param name="execution">The job execution to create.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing job execution record.
    /// </summary>
    /// <param name="execution">The job execution to update.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(JobExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a job execution by its unique identifier.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The job execution if found; otherwise, null.</returns>
    Task<JobExecution?> GetByIdAsync(Guid executionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated execution history for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="skip">The number of records to skip (for pagination).</param>
    /// <param name="take">The number of records to retrieve.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of job executions ordered by StartTime descending.</returns>
    Task<IEnumerable<JobExecution>> GetHistoryAsync(
        string jobId, 
        int skip, 
        int take, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently running executions for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of running job executions.</returns>
    Task<IEnumerable<JobExecution>> GetRunningExecutionsAsync(
        string jobId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes execution records older than the specified cutoff date.
    /// </summary>
    /// <param name="cutoffDate">The date before which all records will be deleted.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recent execution for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The most recent job execution if found; otherwise, null.</returns>
    Task<JobExecution?> GetLastExecutionAsync(string jobId, CancellationToken cancellationToken = default);
}
