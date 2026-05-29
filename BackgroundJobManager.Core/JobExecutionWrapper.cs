using BackgroundJobManager.Abstractions;
using Microsoft.Extensions.Logging;

namespace BackgroundJobManager.Core;

/// <summary>
/// Internal helper that wraps IJob execution to track lifecycle and capture results.
/// </summary>
internal class JobExecutionWrapper(
    IJobExecutionRepository executionRepository,
    IDistributedJobLock distributedLock,
    ILogger<JobExecutionWrapper> logger,
    JobManagementOptions options)
{
    private readonly IJobExecutionRepository _executionRepository = executionRepository;
    private readonly IDistributedJobLock _distributedLock = distributedLock;
    private readonly ILogger<JobExecutionWrapper> _logger = logger;
    private readonly JobManagementOptions _options = options;

    /// <summary>
    /// Executes a job with full lifecycle tracking: lock acquisition, execution, history recording.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="configuration">The job configuration.</param>
    /// <param name="scheduledTime">The time the job was scheduled to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteWithTrackingAsync(
        IJob job,
        JobConfiguration configuration,
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken)
    {
        var jobId = configuration.Id;
        var instanceId = _options.InstanceId;

        // Attempt to acquire distributed lock
        var lockAcquired = await _distributedLock.AcquireLockAsync(
            jobId,
            instanceId,
            _options.LockDuration,
            cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogInformation(
                "Job {JobId} execution skipped - lock already held by another instance.",
                jobId);

            // Record as skipped
            var skippedExecution = new JobExecution
            {
                JobConfigurationId = jobId,
                ScheduledTime = scheduledTime,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow,
                Status = JobExecutionStatus.Skipped,
                ExecutedByInstance = instanceId,
                ErrorMessage = "Lock held by another instance"
            };
            skippedExecution.CalculateDuration();
            await _executionRepository.CreateAsync(skippedExecution, cancellationToken);
            return;
        }

        // Create execution record
        var execution = new JobExecution
        {
            JobConfigurationId = jobId,
            ScheduledTime = scheduledTime,
            StartTime = DateTimeOffset.UtcNow,
            Status = JobExecutionStatus.Running,
            ExecutedByInstance = instanceId
        };

        try
        {
            await _executionRepository.CreateAsync(execution, cancellationToken);

            _logger.LogInformation(
                "Starting execution of job {JobId} (Execution ID: {ExecutionId})",
                jobId,
                execution.Id);

            // Execute the job with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(_options.JobExecutionTimeoutMinutes));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await job.ExecuteAsync(linkedCts.Token);

            // Mark as successful
            execution.EndTime = DateTimeOffset.UtcNow;
            execution.Status = JobExecutionStatus.Success;
            execution.CalculateDuration();

            _logger.LogInformation(
                "Job {JobId} completed successfully in {DurationMs}ms",
                jobId,
                execution.DurationMs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown
            execution.EndTime = DateTimeOffset.UtcNow;
            execution.Status = JobExecutionStatus.Failed;
            execution.ErrorMessage = "Job execution cancelled due to application shutdown";
            execution.CalculateDuration();

            _logger.LogWarning(
                "Job {JobId} cancelled due to application shutdown",
                jobId);
        }
        catch (Exception ex)
        {
            // Job execution failed
            execution.EndTime = DateTimeOffset.UtcNow;
            execution.Status = JobExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            execution.StackTrace = ex.ToString();
            execution.CalculateDuration();

            _logger.LogError(
                ex,
                "Job {JobId} failed after {DurationMs}ms: {ErrorMessage}",
                jobId,
                execution.DurationMs,
                ex.Message);
        }
        finally
        {
            // Update execution record
            await _executionRepository.UpdateAsync(execution, cancellationToken);

            // Release distributed lock
            await _distributedLock.ReleaseLockAsync(jobId, instanceId, cancellationToken);
        }
    }
}
