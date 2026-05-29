using BackgroundJobManager.Abstractions;
using Cronos;

namespace BackgroundJobManager.Core;

/// <summary>
/// Implementation of cron schedule evaluation using the Cronos library.
/// Provides validation, next occurrence calculation, and execution timing logic.
/// </summary>
public class CronScheduleEvaluator : ICronScheduleEvaluator
{
    /// <inheritdoc />
    public bool IsValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        try
        {
            CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset fromTime, string timeZoneId)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            // Convert to the target timezone for evaluation
            var localTime = TimeZoneInfo.ConvertTime(fromTime, timeZone);
            var nextOccurrence = cron.GetNextOccurrence(localTime.DateTime, timeZone);

            if (nextOccurrence.HasValue)
            {
                // Convert back to DateTimeOffset with the correct timezone offset
                var offset = timeZone.GetUtcOffset(nextOccurrence.Value);
                return new DateTimeOffset(nextOccurrence.Value, offset);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool ShouldExecuteNow(string cronExpression, DateTimeOffset lastExecution, DateTimeOffset now, string timeZoneId)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

            // Convert to target timezone
            var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
            var localLastExecution = TimeZoneInfo.ConvertTime(lastExecution, timeZone);

            // If never executed, check if now is past the first scheduled occurrence
            if (lastExecution == DateTimeOffset.MinValue)
            {
                var firstOccurrence = cron.GetNextOccurrence(DateTime.UtcNow.AddYears(-100), timeZone);
                return firstOccurrence.HasValue && localNow.DateTime >= firstOccurrence.Value;
            }

            // Get the next occurrence after the last execution
            var nextOccurrence = cron.GetNextOccurrence(localLastExecution.DateTime, timeZone);

            // Job should execute if the next occurrence is in the past (relative to now)
            return nextOccurrence.HasValue && localNow.DateTime >= nextOccurrence.Value;
        }
        catch
        {
            return false;
        }
    }
}
