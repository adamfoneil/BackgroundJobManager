namespace Abstractions;

/// <summary>
/// use this in your UI to get info about the background jobs, e.g. for an admin dashboard
/// </summary>
public interface ISwitchboardUI
{
    Task<IDictionary<string, (NextRunInfo? NextRun, LastRunInfo? LastRun)>> GetAdminViewAsync();
}
