namespace ManagedBackgroundJob.Abstractions;

/// <summary>
/// Simple interface for evaluating cron schedules
/// Implement this with a library like Cronos or NCrontab
/// </summary>
public interface ICronEvaluator
{
    /// <summary>
    /// Gets the next occurrence of a cron expression
    /// </summary>
    DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset from, string timeZoneId);

    /// <summary>
    /// Validates if a cron expression is valid
    /// </summary>
    bool IsValid(string cronExpression);
}
