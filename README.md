# Background Job Manager

A lightweight, EF Core-based background job management system for .NET 10 that provides simple job scheduling, execution tracking, and runtime configuration through a clean "switchboard" pattern.

## Features

- ✅ **Simple Base Class** - Extend `SwitchboardBackgroundService` to create scheduled background jobs
- ✅ **Dynamic Configuration** - Enable/disable jobs and configure schedules at runtime via database
- ✅ **Execution Tracking** - Automatic logging of job runs with start/finish times, success/failure status, and exception details
- ✅ **Schedule Flexibility** - Support for simple interval notation ("5m", "1h") with extensibility for full cron expressions
- ✅ **Timezone Support** - Schedule jobs in any timezone
- ✅ **Database Agnostic** - Built on EF Core, works with SQL Server, PostgreSQL, SQLite, or any EF provider
- ✅ **Structured Logging** - Automatic correlation IDs (RunId) for tracking individual executions
- ✅ **Anti-Concurrent** - Built-in jitter and status checks prevent overlapping job executions

## Architecture

The project consists of two main components:

### 1. **Abstractions** (Core abstractions and base classes)
- `ISwitchboard` - Central interface for managing job state and execution logging
- `SwitchboardBackgroundService` - Abstract base class for implementing background jobs
- Supporting types: `ExecuteResult`, `NextRunInfo`, `LastRunInfo`, `ServiceStatus`

### 2. **Services** (EF Core implementation)
- `SwitchboardService` - Concrete implementation of `ISwitchboard` using Entity Framework Core
- `ManagedJobDbContext` - EF Core context with `JobConfiguration` and `JobRun` entities
- `ICronEvaluator` / `DefaultCronEvaluator` - Extensible cron/schedule evaluation
- **Entities**: `JobConfiguration` (job settings), `JobRun` (execution history)


## Quick Start

### 1. Install Dependencies

Add the project references to your ASP.NET Core or console application:

```xml
<ItemGroup>
  <ProjectReference Include="..\BackgroundJobManager\Abstractions\Abstractions.csproj" />
  <ProjectReference Include="..\BackgroundJobManager\Services\Services.csproj" />
</ItemGroup>
```

### 2. Create a Background Job

Implement your job by extending `SwitchboardBackgroundService`:

```csharp
using Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class EmailReportJob : SwitchboardBackgroundService
{
	private readonly IEmailService _emailService;

	public EmailReportJob(
		ILogger<EmailReportJob> logger,
		ISwitchboard switchboard,
		IEmailService emailService)
		: base(logger, switchboard)
	{
		_emailService = emailService;
	}

	protected override string ServiceType => "EmailReportJob";

	protected override async Task<ExecuteResult> ExecuteInternalAsync(
		string runId, 
		CancellationToken stoppingToken)
	{
		try
		{
			Logger.LogInformation("Generating email report...");

			var report = await GenerateReportAsync(stoppingToken);
			await _emailService.SendAsync("reports@company.com", "Daily Report", report);

			return new ExecuteResult(true, "Report sent successfully");
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Failed to send report");
			return new ExecuteResult(false, "Failed to send report", ex);
		}
	}

	private async Task<string> GenerateReportAsync(CancellationToken ct)
	{
		// Your report generation logic
		await Task.Delay(100, ct);
		return "Report content";
	}
}
```

### 3. Configure Services and Database

In your `Program.cs`:

```csharp
using ManagedBackgroundJob.Abstractions;
using ManagedBackgroundJob.Abstractions.Data;
using ManagedBackgroundJob.Abstractions.Services;
using Microsoft.EntityFrameworkCore;
using Services;

var builder = WebApplication.CreateBuilder(args);

// Register the database context
builder.Services.AddDbContext<ManagedJobDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register the switchboard service
builder.Services.AddScoped<ISwitchboard, SwitchboardService>();

// Register the cron evaluator (use default or implement custom)
builder.Services.AddSingleton<ICronEvaluator, DefaultCronEvaluator>();

// Register your background jobs as hosted services
builder.Services.AddHostedService<EmailReportJob>();

var app = builder.Build();

// Apply migrations on startup (optional, for development)
using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<ManagedJobDbContext>();
	await dbContext.Database.MigrateAsync();
}

app.Run();
```

### 4. Create and Apply Migrations

```bash
# Add migration
dotnet ef migrations add InitialCreate --project YourProject

# Update database
dotnet ef database update --project YourProject
```

### 5. Configure Job Schedule (via Database)

Jobs are automatically created with default settings (enabled, no schedule = continuous execution). To configure schedules:

```sql
-- Run every 5 minutes
UPDATE JobConfigurations 
SET CronSchedule = '5m'
WHERE ServiceName = 'EmailReportJob';

-- Run every hour
UPDATE JobConfigurations 
SET CronSchedule = '1h'
WHERE ServiceName = 'EmailReportJob';

-- Disable a job
UPDATE JobConfigurations 
SET IsEnabled = 0
WHERE ServiceName = 'EmailReportJob';

-- Set timezone
UPDATE JobConfigurations 
SET TimeZoneId = 'America/New_York'
WHERE ServiceName = 'EmailReportJob';
```

## Cron Expression Examples

```
"0 0 8 * * *"       - Every day at 8:00 AM
"0 */15 * * * *"    - Every 15 minutes
"0 0 0 * * MON"     - Every Monday at midnight
"0 0 12 1 * *"      - First day of every month at noon
"0 30 9 * * MON-FRI"- Weekdays at 9:30 AM
```

Cron format: `second minute hour day month dayOfWeek`

## Database Schema

**JobConfiguration Table:**
- Id (string, PK)
- JobTypeName (string)
- DisplayName (string)
- CronSchedule (string)
- Enabled (bool)
- TimeZoneId (string)
- RetentionDays (int, nullable)
- Metadata (JSON, nullable)

**JobExecution Table:**
- Id (guid, PK)
- JobConfigurationId (string, FK)
- ScheduledTime (datetimeoffset)
- StartTime (datetimeoffset)
- EndTime (datetimeoffset, nullable)
- Status (int/enum)
- ErrorMessage (string, nullable)
- StackTrace (text, nullable)
- ExecutedByInstance (string, nullable)
- DurationMs (long, nullable)

**JobLocks Table (for distributed locking):**
- JobId (string, PK)
- InstanceId (string)
- ExpiresAt (datetime)

## Configuration Options

| Property | Default | Description |
|----------|---------|-------------|
| `PollingIntervalSeconds` | 30 | How often to check for jobs to execute |
| `DefaultHistoryRetentionDays` | 30 | Days to retain execution history |
| `JobExecutionTimeoutMinutes` | 60 | Maximum job execution time before timeout |
| `LockDuration` | 5 minutes | How long distributed locks are held |
| `InstanceId` | Machine name | Unique identifier for this instance |

## Best Practices

1. **Idempotency** - Design jobs to be safely re-executable in case of failures
2. **Timeouts** - Keep jobs under the timeout threshold or implement lock renewal
3. **Error Handling** - Jobs should catch and log exceptions; the framework will capture them
4. **Database Indexes** - Index `JobConfigurationId`, `StartTime`, and `Status` in JobExecution table
5. **Clock Sync** - Ensure NTP is configured in distributed environments for accurate scheduling
6. **Testing** - Mock `IJobConfigurationRepository`, `IJobExecutionRepository`, and `ICronScheduleEvaluator` for unit tests

## Future Enhancements

- Retry policies with exponential backoff
- Job dependency chains
- Webhook notifications on failure
- Prometheus metrics exporter
- Admin UI (Blazor)
- Job parameters/configuration injection

## License

[Your License Here]
