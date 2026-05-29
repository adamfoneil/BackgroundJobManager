namespace Abstractions;

public enum ServiceStatus
{
    Scheduled,
    Disabled,
    Running  
}

public record NextRunInfo(
    ServiceStatus Status,
    DateTime? DateTimeUtc);

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
    Task<string> LogStartAsync(string serviceType);    
    /// <summary>
    /// should calculate next run time
    /// </summary>
    Task LogResultAsync(string runId, string serviceType, LastRunInfo info);
    IDictionary<string, NextRunInfo> Schedule { get; }
    IDictionary<string, LastRunInfo> Results { get; }
}
