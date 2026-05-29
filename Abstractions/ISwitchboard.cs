namespace Abstractions;

public interface ISwitchboard
{
    Task EnableAsync(string serviceType);
    Task DisableAsync(string serviceType);
    Task<bool> IsEnabledAsync(string serviceType);
    Task<bool> ShouldRunNowAsync(string serviceType);
    Task<string> StartAsync(string serviceType);
    Task<bool> IsStartedAsync(string serviceType);
    Task FinishAsync(string runId, string serviceType);
    Task LogResultAsync(string runId, ExecuteResult result);
}
