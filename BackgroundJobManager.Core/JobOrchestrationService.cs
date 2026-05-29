using BackgroundJobManager.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundJobManager.Core;

/// <summary>
/// Background service that orchestrates job execution on a polling loop.
/// Loads configurations, evaluates schedules, and triggers job execution.
/// </summary>
public class JobOrchestrationService(
    IServiceProvider serviceProvider,
    ILogger<JobOrchestrationService> logger,
    JobManagementOptions options) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<JobOrchestrationService> _logger = logger;
    private readonly JobManagementOptions _options = options;
    private DateTime _lastCleanupTime = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Job Orchestration Service started on instance {InstanceId}. Polling interval: {PollingInterval}s",
            _options.InstanceId,
            _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobsAsync(stoppingToken);

                // Run history cleanup once per day
                if ((DateTime.UtcNow - _lastCleanupTime).TotalHours >= 24)
                {
                    await CleanupHistoryAsync(stoppingToken);
                    _lastCleanupTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job orchestration loop: {ErrorMessage}", ex.Message);
            }

            // Wait for the next polling interval
            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Job Orchestration Service stopped.");
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var configRepository = scope.ServiceProvider.GetRequiredService<IJobConfigurationRepository>();
        var executionRepository = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
        var cronEvaluator = scope.ServiceProvider.GetRequiredService<ICronScheduleEvaluator>();

        // Load all enabled job configurations
        var configurations = await configRepository.GetAllAsync(cancellationToken);
        var enabledConfigs = configurations.Where(c => c.Enabled).ToList();

        _logger.LogDebug("Evaluating {Count} enabled job(s)", enabledConfigs.Count);

        var now = DateTimeOffset.UtcNow;

        foreach (var config in enabledConfigs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Validate cron expression
                if (!cronEvaluator.IsValidCronExpression(config.CronSchedule))
                {
                    _logger.LogWarning(
                        "Job {JobId} has invalid cron expression: {CronSchedule}",
                        config.Id,
                        config.CronSchedule);
                    continue;
                }

                // Get last execution time
                var lastExecution = await executionRepository.GetLastExecutionAsync(config.Id, cancellationToken);
                var lastExecutionTime = lastExecution?.EndTime ?? DateTimeOffset.MinValue;

                // Check if job should execute now
                var shouldExecute = cronEvaluator.ShouldExecuteNow(
                    config.CronSchedule,
                    lastExecutionTime,
                    now,
                    config.TimeZoneId);

                if (shouldExecute)
                {
                    _logger.LogInformation(
                        "Job {JobId} is due for execution (Last run: {LastRun})",
                        config.Id,
                        lastExecutionTime == DateTimeOffset.MinValue ? "Never" : lastExecutionTime.ToString());

                    // Execute job in a fire-and-forget manner (non-blocking)
                    _ = Task.Run(async () => await ExecuteJobAsync(config, now, cancellationToken), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error evaluating job {JobId}: {ErrorMessage}",
                    config.Id,
                    ex.Message);
            }
        }
    }

    private async Task ExecuteJobAsync(
        JobConfiguration configuration,
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken)
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
                    configuration.Id);
                return;
            }

            var job = scope.ServiceProvider.GetRequiredService(jobType) as IJob;
            if (job == null)
            {
                _logger.LogError(
                    "Type {JobTypeName} does not implement IJob for job {JobId}",
                    configuration.JobTypeName,
                    configuration.Id);
                return;
            }

            // Execute with tracking
            await executionWrapper.ExecuteWithTrackingAsync(
                job,
                configuration,
                scheduledTime,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Fatal error executing job {JobId}: {ErrorMessage}",
                configuration.Id,
                ex.Message);
        }
    }

    private async Task CleanupHistoryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting job execution history cleanup");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var configRepository = scope.ServiceProvider.GetRequiredService<IJobConfigurationRepository>();
            var executionRepository = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();

            var configurations = await configRepository.GetAllAsync(cancellationToken);

            foreach (var config in configurations)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var retentionDays = config.RetentionDays ?? _options.DefaultHistoryRetentionDays;
                var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

                await executionRepository.DeleteOlderThanAsync(cutoffDate, cancellationToken);
            }

            _logger.LogInformation("Job execution history cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during history cleanup: {ErrorMessage}", ex.Message);
        }
    }
}
