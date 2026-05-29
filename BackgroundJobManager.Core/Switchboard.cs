using BackgroundJobManager.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BackgroundJobManager.Core;

/// <summary>
/// Implementation of ISwitchboard that coordinates job management operations.
/// Provides high-level API for managing job configurations and querying execution history.
/// </summary>
public class Switchboard : ISwitchboard
{
    private readonly IJobConfigurationRepository _configRepository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly ICronScheduleEvaluator _cronEvaluator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Switchboard> _logger;
    private readonly JobManagementOptions _options;

    public Switchboard(
        IJobConfigurationRepository configRepository,
        IJobExecutionRepository executionRepository,
        ICronScheduleEvaluator cronEvaluator,
        IServiceProvider serviceProvider,
        ILogger<Switchboard> logger,
        JobManagementOptions options)
    {
        _configRepository = configRepository;
        _executionRepository = executionRepository;
        _cronEvaluator = cronEvaluator;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<JobConfiguration>> GetJobConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        return await _configRepository.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JobConfiguration?> GetJobConfigurationAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await _configRepository.GetByIdAsync(jobId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateJobConfigurationAsync(JobConfiguration configuration, CancellationToken cancellationToken = default)
    {
        // Validate cron expression before persisting
        if (!_cronEvaluator.IsValidCronExpression(configuration.CronSchedule))
        {
            throw new ArgumentException(
                $"Invalid cron expression: {configuration.CronSchedule}",
                nameof(configuration));
        }

        // Validate timezone
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Invalid timezone ID: {configuration.TimeZoneId}",
                nameof(configuration),
                ex);
        }

        await _configRepository.UpsertAsync(configuration, cancellationToken);

        _logger.LogInformation(
            "Job configuration updated: {JobId}, Enabled: {Enabled}, Schedule: {Schedule}",
            configuration.Id,
            configuration.Enabled,
            configuration.CronSchedule);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<JobExecution>> GetJobExecutionHistoryAsync(
        string jobId,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        var skip = (pageNumber - 1) * pageSize;
        return await _executionRepository.GetHistoryAsync(jobId, skip, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetNextScheduledTimeAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var configuration = await _configRepository.GetByIdAsync(jobId, cancellationToken);
        if (configuration == null || !configuration.Enabled)
        {
            return null;
        }

        if (!_cronEvaluator.IsValidCronExpression(configuration.CronSchedule))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return _cronEvaluator.GetNextOccurrence(configuration.CronSchedule, now, configuration.TimeZoneId);
    }

    /// <inheritdoc />
    public async Task TriggerJobNowAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var configuration = await _configRepository.GetByIdAsync(jobId, cancellationToken);
        if (configuration == null)
        {
            throw new InvalidOperationException($"Job configuration not found: {jobId}");
        }

        _logger.LogInformation("Manual trigger requested for job {JobId}", jobId);

        // Execute the job immediately in a background task
        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var executionWrapper = scope.ServiceProvider.GetRequiredService<JobExecutionWrapper>();

            try
            {
                // Resolve the job by type name
                var jobType = Type.GetType(configuration.JobTypeName);
                if (jobType == null)
                {
                    _logger.LogError(
                        "Cannot resolve job type {JobTypeName} for job {JobId}",
                        configuration.JobTypeName,
                        jobId);
                    return;
                }

                var job = scope.ServiceProvider.GetRequiredService(jobType) as IJob;
                if (job == null)
                {
                    _logger.LogError(
                        "Type {JobTypeName} does not implement IJob for job {JobId}",
                        configuration.JobTypeName,
                        jobId);
                    return;
                }

                // Execute with tracking
                await executionWrapper.ExecuteWithTrackingAsync(
                    job,
                    configuration,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during manual trigger of job {JobId}: {ErrorMessage}",
                    jobId,
                    ex.Message);
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<JobExecution?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await _executionRepository.GetLastExecutionAsync(jobId, cancellationToken);
    }
}
