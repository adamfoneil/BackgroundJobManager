namespace Abstractions;

public enum ServiceStatus
{
    Scheduled,
    Disabled,
    Running  
}

public record NextRunInfo(
    ServiceStatus Status,    
    DateTime? StartUtc);

public record LastRunInfo(
    DateTime StartedUtc,
    DateTime FinishedUtc,
    TimeSpan Duration,
    ExecuteResult Result);

/// <summary>
/// used with SwitchboardBackgroundService to manage singleton instances of background jobs
/// </summary>
public interface ISwitchboard
{
    /// <summary>
    /// generate RunId (CorrelationId) and mark job as Running
    /// </summary>
    Task<string> LogStartAsync(string serviceType, string machineName);
    DateTime NextRunDateTimeUtc(string serviceType);
    Task LogResultAsync(string runId, string serviceType, LastRunInfo info, DateTime nextRun);
    /// <summary>
    /// for UIs, not for 
    /// </summary>
    Task<NextRunInfo?> GetNextRunAsync(string serviceType);
    Task<LastRunInfo?> GetResultsAsync(string serviceType);
    Task<IDictionary<string, (NextRunInfo? NextRun, LastRunInfo? LastRun)>> GetAdminViewAsync();
}
