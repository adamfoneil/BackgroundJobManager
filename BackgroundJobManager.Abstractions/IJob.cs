namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Defines a contract for a background job that can be scheduled and executed.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Gets the unique identifier for this job. Typically the type name or a descriptive constant.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Executes the job logic asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
