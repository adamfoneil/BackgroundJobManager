namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Interface for distributed locking mechanism to prevent concurrent execution of the same job across multiple instances.
/// </summary>
public interface IDistributedJobLock
{
    /// <summary>
    /// Attempts to acquire a distributed lock for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier to lock.</param>
    /// <param name="instanceId">The identifier of the instance attempting to acquire the lock (e.g., hostname).</param>
    /// <param name="lockDuration">The duration for which the lock should be held.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the lock was successfully acquired; otherwise, false.</returns>
    Task<bool> AcquireLockAsync(
        string jobId, 
        string instanceId, 
        TimeSpan lockDuration, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously acquired lock for a specific job.
    /// </summary>
    /// <param name="jobId">The unique job identifier to unlock.</param>
    /// <param name="instanceId">The identifier of the instance that holds the lock.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReleaseLockAsync(
        string jobId, 
        string instanceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews an existing lock to extend its duration. Used for long-running jobs.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="instanceId">The identifier of the instance that holds the lock.</param>
    /// <param name="lockDuration">The new duration to extend the lock by.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the lock was successfully renewed; otherwise, false.</returns>
    Task<bool> RenewLockAsync(
        string jobId, 
        string instanceId, 
        TimeSpan lockDuration, 
        CancellationToken cancellationToken = default);
}
