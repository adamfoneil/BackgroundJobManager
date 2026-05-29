namespace Abstractions;

public interface ISwitchboard
{
    Task EnableAsync(string serviceType);
    Task DisableAsync(string serviceType);
    Task<bool> IsEnabledAsync(string serviceType);
    Task<bool> ShouldRunNowAsync(string serviceType);
    Task<string> LogStartAsync(string serviceType);
    Task<bool> IsStartedAsync(string serviceType);
    Task LogFinishAsync(string runId, string serviceType);
    Task LogResultAsync(string runId, ExecuteResult result);
}
