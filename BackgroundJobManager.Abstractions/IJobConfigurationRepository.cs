namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Repository interface for persisting and retrieving job configurations.
/// </summary>
public interface IJobConfigurationRepository
{
    /// <summary>
    /// Retrieves all job configurations from the repository.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A collection of all job configurations.</returns>
    Task<IEnumerable<JobConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a job configuration by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The job configuration if found; otherwise, null.</returns>
    Task<JobConfiguration?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a job configuration.
    /// </summary>
    /// <param name="configuration">The job configuration to persist.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(JobConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a job configuration by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);
}
