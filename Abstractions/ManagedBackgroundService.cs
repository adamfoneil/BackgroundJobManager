using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Abstractions;

public record ExecuteResult(
    bool Success,
    string? Message = null,
    Exception? Exception = null);

public abstract class ManagedBackgroundService(
    ILogger<ManagedBackgroundService> logger,
    ISwitchboard switchboard) : BackgroundService
{
    private readonly ILogger<ManagedBackgroundService> _logger = logger;

    public ISwitchboard Switchboard { get; } = switchboard;

    protected abstract Task<ExecuteResult> ExecuteInternalAsync(string runId, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var serviceType = GetType().Name;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!await Switchboard.IsEnabledAsync(serviceType)) return;
            if (await Switchboard.IsStartedAsync(serviceType)) return;
            if (!await Switchboard.ShouldRunNowAsync(serviceType)) return;

            string runId = await Switchboard.StartAsync(serviceType);
            using var _ = _logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });
            _logger.LogDebug("Starting execution of {ServiceName} with RunId {RunId}.", serviceType, runId);

            var sw = Stopwatch.StartNew();

            ExecuteResult result = new(false);
            try
            {
                result = await ExecuteInternalAsync(runId, stoppingToken);
                await Switchboard.LogResultAsync(runId, result);
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Unhandled error in {ServiceName}.", serviceType);        
            }
            finally
            {
                sw.Stop();
                await Switchboard.FinishAsync(runId, serviceType);                
                _logger.LogInformation("{ServiceName} execution {result} in {ElapsedSeconds}s.", serviceType, result.Success ? "succeeded" : "failed", sw.Elapsed.TotalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
