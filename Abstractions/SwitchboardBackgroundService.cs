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
    /// this is where your job implementation goes
    /// </summary>
    protected abstract Task<ExecuteResult> ExecuteInternalAsync(string runId, CancellationToken stoppingToken);

    protected virtual string ServiceType => GetType().Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (Switchboard.Schedule.TryGetValue(ServiceType, out var nextRun))
            {
                if (nextRun.Status is ServiceStatus.Disabled or ServiceStatus.Running) return;
                if (nextRun.DateTimeUtc.HasValue && nextRun.DateTimeUtc.Value > DateTime.UtcNow) return;
            }

            var start = DateTime.UtcNow;
            string runId = await Switchboard.LogStartAsync(ServiceType);
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
                await Switchboard.LogResultAsync(runId, ServiceType, new(start, DateTime.UtcNow, sw.Elapsed, result));
                _logger.LogInformation("{ServiceName} execution {result} in {ElapsedSeconds}s.", ServiceType, result.Success ? "succeeded" : "failed", sw.Elapsed.TotalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
