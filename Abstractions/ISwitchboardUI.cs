namespace Abstractions;

/// <summary>
/// use this in your UI to get info about the background jobs, e.g. for an admin dashboard
/// </summary>
public interface ISwitchboardUI
{
    Task<IDictionary<string, (NextRunInfo? NextRun, LastRunInfo? LastRun)>> GetAdminViewAsync();
    /// <summary>
    /// mark service as Scheduled
    /// </summary>
    Task EnableAsync(string serviceType);
    /// <summary>
    /// mark service as Disabled
    /// </summary>
    Task DisableAsync(string serviceType);
    /// <summary>
    /// run service immediately (set NextRun to now). Note that if the service is already running, this will not do anything until the current run 
    /// is finished and the next run is scheduled. Use with caution to avoid multiple instances of the same service running at the same time.
    /// </summary>
    Task RunNowAsync(string serviceType);
}
