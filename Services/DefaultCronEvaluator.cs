using Microsoft.Extensions.Logging;

namespace ManagedBackgroundJob.Abstractions.Services;

/// <summary>
/// Simple default implementation of ICronEvaluator
/// In production, replace this with a proper cron library like Cronos or NCrontab
/// This implementation only handles basic interval-based schedules
/// </summary>
public class DefaultCronEvaluator : ICronEvaluator
{
    private readonly ILogger<DefaultCronEvaluator> _logger;

    public DefaultCronEvaluator(ILogger<DefaultCronEvaluator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset from, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return null;
        }

        // This is a simplified implementation
        // For production use, integrate a library like Cronos:
        // var cron = CronExpression.Parse(cronExpression);
        // return cron.GetNextOccurrence(from, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

        // Simple interval parsing (e.g., "5m", "1h", "30s")
        if (TryParseInterval(cronExpression, out TimeSpan interval))
        {
            return from.Add(interval);
        }

        _logger.LogWarning(
            "Could not parse cron expression: {CronExpression}. Consider using a proper cron library.",
            cronExpression);

        // Default to 5 minutes if we can't parse
        return from.AddMinutes(5);
    }

    /// <inheritdoc />
    public bool IsValid(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return false;
        }

        // Check if it's a simple interval
        if (TryParseInterval(cronExpression, out _))
        {
            return true;
        }

        // For production, validate with a proper cron library:
        // try
        // {
        //     CronExpression.Parse(cronExpression);
        //     return true;
        // }
        // catch
        // {
        //     return false;
        // }

        // For now, accept anything non-empty as potentially valid
        _logger.LogWarning(
            "Cannot fully validate cron expression: {CronExpression}. Consider using a proper cron library.",
            cronExpression);
        return true;
    }

    /// <summary>
    /// Tries to parse simple interval notation (e.g., "5m", "1h", "30s")
    /// </summary>
    private static bool TryParseInterval(string expression, out TimeSpan interval)
    {
        interval = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        expression = expression.Trim().ToLowerInvariant();

        // Try to parse formats like "5m", "1h", "30s"
        if (expression.Length < 2)
        {
            return false;
        }

        char unit = expression[^1];
        string numberPart = expression[..^1];

        if (!int.TryParse(numberPart, out int value))
        {
            return false;
        }

        interval = unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            'd' => TimeSpan.FromDays(value),
            _ => TimeSpan.Zero
        };

        return interval > TimeSpan.Zero;
    }
}
