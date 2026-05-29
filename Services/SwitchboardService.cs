using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ManagedBackgroundJob.Abstractions.Data;
using ManagedBackgroundJob.Abstractions.Entities;
using Abstractions;

namespace ManagedBackgroundJob.Abstractions.Services;

/// <summary>
/// Default implementation of ISwitchboard using EF Core for persistence
/// </summary>
public class SwitchboardService(
    ManagedJobDbContext dbContext,
    ICronEvaluator cronEvaluator,
    ILogger<SwitchboardService> logger) : ISwitchboard
{
    private readonly ManagedJobDbContext _dbContext = dbContext;
    private readonly ICronEvaluator _cronEvaluator = cronEvaluator;
    private readonly ILogger<SwitchboardService> _logger = logger;

    /// <inheritdoc />
    public async Task EnableAsync(string serviceName)
    {
        var config = GetOrCreateConfiguration(serviceName);
        config.IsEnabled = true;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Enabled service: {ServiceName}", serviceName);
    }

    /// <inheritdoc />
    public async Task DisableAsync(string serviceName)
    {
        var config = GetOrCreateConfiguration(serviceName);
        config.IsEnabled = false;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Disabled service: {ServiceName}", serviceName);
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string serviceName)
    {
        var config = await _dbContext.JobConfigurations.FindAsync(serviceName);
        return config?.IsEnabled ?? true; // Default to enabled if no configuration exists
    }

    /// <inheritdoc />
    public async Task<bool> ShouldRunNowAsync(string serviceName)
    {
        var config = GetOrCreateConfiguration(serviceName);

        // If no cron schedule, always run (continuous mode)
        if (string.IsNullOrWhiteSpace(config.CronSchedule))
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;

        // If we haven't calculated next run time yet, or it's stale, recalculate
        if (!config.NextScheduledRun.HasValue || 
            !config.LastScheduleCheck.HasValue ||
            config.LastScheduleCheck.Value < now.AddMinutes(-5))
        {
            config.NextScheduledRun = _cronEvaluator.GetNextOccurrence(
                config.CronSchedule,
                now,
                config.TimeZoneId);
            config.LastScheduleCheck = now;
            _dbContext.SaveChanges();
        }

        // Run if the next scheduled time has passed
        return config.NextScheduledRun.HasValue && config.NextScheduledRun.Value <= now;
    }

    /// <inheritdoc />
    public async Task<string> StartAsync(string serviceName)
    {
        // Generate a unique run ID
        string runId = $"{serviceName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";

        var jobRun = new JobRun
        {
            RunId = runId,
            ServiceName = serviceName,
            StartedAt = DateTimeOffset.UtcNow
        };

        _dbContext.JobRuns.Add(jobRun);
        await _dbContext.SaveChangesAsync();

        // Update the next scheduled run time if using cron
        var config = GetOrCreateConfiguration(serviceName);
        if (!string.IsNullOrWhiteSpace(config.CronSchedule))
        {
            var now = DateTimeOffset.UtcNow;
            config.NextScheduledRun = _cronEvaluator.GetNextOccurrence(
                config.CronSchedule,
                now,
                config.TimeZoneId);
            config.LastScheduleCheck = now;
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("Started job run: {RunId} for service: {ServiceName}", runId, serviceName);

        return runId;
    }

    /// <inheritdoc />
    public async Task<bool> IsStartedAsync(string serviceName) => await _dbContext.JobRuns.AnyAsync(r => r.ServiceName == serviceName && !r.FinishedAt.HasValue);

    /// <inheritdoc />
    public async Task FinishAsync(string runId, string serviceName)
    {
        var jobRun = await _dbContext.JobRuns.FindAsync(runId);
        if (jobRun == null)
        {
            _logger.LogWarning("Attempted to finish non-existent run: {RunId}", runId);
            return;
        }

        jobRun.FinishedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        var duration = jobRun.FinishedAt.Value - jobRun.StartedAt;
        _logger.LogInformation(
            "Finished job run: {RunId} for service: {ServiceName} in {Duration}s",
            runId,
            serviceName,
            duration.TotalSeconds);
    }

    /// <inheritdoc />
    public async Task LogResultAsync(string runId, ExecuteResult result)
    {
        var jobRun = await _dbContext.JobRuns.FindAsync(runId);
        if (jobRun == null)
        {
            _logger.LogWarning("Attempted to log result for non-existent run: {RunId}", runId);
            return;
        }

        jobRun.Success = result.Success;
        jobRun.Message = result.Message;

        if (result.Exception != null)
        {
            jobRun.ExceptionDetails = $"{result.Exception.GetType().Name}: {result.Exception.Message}\n{result.Exception.StackTrace}";
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogDebug(
            "Logged result for run: {RunId}, Success: {Success}",
            runId,
            result.Success);
    }

    /// <summary>
    /// Gets or creates a job configuration for the specified service
    /// </summary>
    private JobConfiguration GetOrCreateConfiguration(string serviceName)
    {
        var config = _dbContext.JobConfigurations.Find(serviceName);
        if (config == null)
        {
            config = new JobConfiguration
            {
                ServiceName = serviceName,
                IsEnabled = true,
                TimeZoneId = "UTC"
            };
            _dbContext.JobConfigurations.Add(config);
            _dbContext.SaveChanges();
        }
        return config;
    }
}
