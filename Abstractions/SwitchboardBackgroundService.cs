using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Abstractions;

public enum ExecuteResultType
{
    // job returned success
    Success,
    // not success, but no exception
    Warning,
    // exception was thrown
    Error
}

public record ExecuteResult(
    bool Success,
    string? Message = null,
    Exception? Exception = null)
{
    public ExecuteResultType ResultType =>
        !Success && Exception is not null ? ExecuteResultType.Error :
        !Success && Exception is null ? ExecuteResultType.Warning :
        ExecuteResultType.Success;

    /// <summary>
    /// additional messages related to the execution, e.g. validation errors, warnings, etc.
    /// that might be inconvenient to get from ILogger
    /// </summary>
    public string[] Messages { get; set; } = [];
}

public abstract class SwitchboardBackgroundService(
    ILogger<SwitchboardBackgroundService> logger,
    ISwitchboard switchboard) : BackgroundService
{
    private readonly ILogger<SwitchboardBackgroundService> _logger = logger;

    public ISwitchboard Switchboard { get; } = switchboard;

    /// <summary>
    /// your implementation of the job goes here. You can log as needed, and return success/failure and messages via the ExecuteResult.
    /// </summary>
    protected abstract Task<ExecuteResult> ExecuteInternalAsync(string runId, CancellationToken stoppingToken);

    /// <summary>
    /// how do we refer to this job in the UI and logs?
    /// </summary>
    protected virtual string ServiceType => GetType().Name;

    /// <summary>
    /// min delay between job runs to prevent multiple instances from running at the same time due to clock skew or other issues with scheduling.
    /// Adjust as needed based on expected job duration and scheduling frequency.
    /// </summary>
    protected virtual int MinLoopDelaySeconds => 10;

    /// <summary>
    /// max delay between job runs to prevent multiple instances from running at the same time due to clock skew or other issues with scheduling.
    /// Adjust as needed based on expected job duration and scheduling frequency.
    /// </summary>
    protected virtual int MaxLoopDelaySeconds => 15;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {        
        while (!stoppingToken.IsCancellationRequested)
        {
            bool allowRun = false;
            var schedule = await Switchboard.GetNextRunAsync(ServiceType);

            if (schedule is not null)
            {
                // if disabled or alerady running, skip
                if (schedule.Status is ServiceStatus.Disabled or ServiceStatus.Running) return;

                // if not scheduled to run now, skip
                if (!schedule.StartUtc.HasValue) return;

                // if scheduled for the future, skip
                if (schedule.StartUtc > DateTime.UtcNow) return;

                allowRun = true;
            }

            // can't run if not in schedule, or if schedule says not to run
            if (!allowRun) return;

            var start = DateTime.UtcNow;
            string runId = await Switchboard.LogStartAsync(ServiceType, Environment.MachineName);
            using var _ = _logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
            _logger.LogDebug("Starting execution of {ServiceName} with RunId {RunId}.", ServiceType, runId);

            var sw = Stopwatch.StartNew();

            ExecuteResult result = new(false);
            try
            {
                result = await ExecuteInternalAsync(runId, stoppingToken);                
            }
            catch (Exception exc)
            {
                result = new(false, exc.Message, exc);
                _logger.LogError(exc, "Unhandled error in {ServiceName}.", ServiceType);        
            }
            finally
            {
                sw.Stop();                
                await Switchboard.LogResultAsync(runId, ServiceType, new(start, DateTime.UtcNow, sw.Elapsed, result), Switchboard.NextRunDateTimeUtc(ServiceType));
                _logger.LogInformation("{ServiceName} execution {result} in {ElapsedSeconds}s.", ServiceType, result.Success ? "succeeded" : "failed", sw.Elapsed.TotalSeconds);
            }

            await Task.Delay(JitterDelay, stoppingToken);
        }
    }

    /// <summary>
    /// delay between core loop runs is random within defined bounds
    /// </summary>
    private TimeSpan JitterDelay => TimeSpan.FromSeconds(Random.Shared.Next(MinLoopDelaySeconds, MaxLoopDelaySeconds));
}
