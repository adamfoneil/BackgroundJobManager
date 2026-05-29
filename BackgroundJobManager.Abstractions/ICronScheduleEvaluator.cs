namespace BackgroundJobManager.Abstractions;

/// <summary>
/// Interface for evaluating cron schedules to determine job execution timing.
/// Provides testability by abstracting the underlying cron library.
/// </summary>
public interface ICronScheduleEvaluator
{
    /// <summary>
    /// Validates whether a cron expression is syntactically correct.
    /// </summary>
    /// <param name="cronExpression">The cron expression to validate.</param>
    /// <returns>True if the cron expression is valid; otherwise, false.</returns>
    bool IsValidCronExpression(string cronExpression);

    /// <summary>
    /// Calculates the next occurrence of a cron schedule from a given time.
    /// </summary>
    /// <param name="cronExpression">The cron expression to evaluate.</param>
    /// <param name="fromTime">The reference time to calculate from.</param>
    /// <param name="timeZoneId">The timezone ID to evaluate the cron expression in (e.g., "UTC", "America/New_York").</param>
    /// <returns>The next occurrence time if calculable; otherwise, null.</returns>
    DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset fromTime, string timeZoneId);

    /// <summary>
    /// Determines whether a job should execute now based on its schedule and last execution time.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining the schedule.</param>
    /// <param name="lastExecution">The timestamp of the last successful execution, or DateTimeOffset.MinValue if never executed.</param>
    /// <param name="now">The current time to evaluate against.</param>
    /// <param name="timeZoneId">The timezone ID for evaluation.</param>
    /// <returns>True if the job should execute now; otherwise, false.</returns>
    bool ShouldExecuteNow(string cronExpression, DateTimeOffset lastExecution, DateTimeOffset now, string timeZoneId);
}
