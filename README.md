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

## Getting Started

### 1. Define a Job

Implement the `IJob` interface:

```csharp
using BackgroundJobManager.Abstractions;

public class SendEmailReportJob : IJob
{
	private readonly IEmailService _emailService;
	private readonly ILogger<SendEmailReportJob> _logger;

	public string JobId => "send-email-report";

	public SendEmailReportJob(IEmailService emailService, ILogger<SendEmailReportJob> logger)
	{
		_emailService = emailService;
		_logger = logger;
	}

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Generating and sending email report");

		var report = await GenerateReportAsync(cancellationToken);
		await _emailService.SendAsync("report@example.com", "Daily Report", report, cancellationToken);

		_logger.LogInformation("Email report sent successfully");
	}

	private async Task<string> GenerateReportAsync(CancellationToken cancellationToken)
	{
		// Your report generation logic
		return "Report content";
	}
}
```

### 2. Implement Repository Interfaces

Implement `IJobConfigurationRepository` and `IJobExecutionRepository` for your chosen database.

**Example: MySQL with Entity Framework Core**

```csharp
using BackgroundJobManager.Abstractions;
using Microsoft.EntityFrameworkCore;

public class JobConfigurationRepository : IJobConfigurationRepository
{
	private readonly JobDbContext _context;

	public JobConfigurationRepository(JobDbContext context)
	{
		_context = context;
	}

	public async Task<IEnumerable<JobConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _context.JobConfigurations.ToListAsync(cancellationToken);
	}

	public async Task<JobConfiguration?> GetByIdAsync(string jobId, CancellationToken cancellationToken = default)
	{
		return await _context.JobConfigurations.FindAsync(new object[] { jobId }, cancellationToken);
	}

	public async Task UpsertAsync(JobConfiguration configuration, CancellationToken cancellationToken = default)
	{
		var existing = await _context.JobConfigurations.FindAsync(new object[] { configuration.Id }, cancellationToken);

		if (existing == null)
		{
			_context.JobConfigurations.Add(configuration);
		}
		else
		{
			_context.Entry(existing).CurrentValues.SetValues(configuration);
		}

		await _context.SaveChangesAsync(cancellationToken);
	}

	public async Task DeleteAsync(string jobId, CancellationToken cancellationToken = default)
	{
		var config = await _context.JobConfigurations.FindAsync(new object[] { jobId }, cancellationToken);
		if (config != null)
		{
			_context.JobConfigurations.Remove(config);
			await _context.SaveChangesAsync(cancellationToken);
		}
	}
}
```

### 3. Implement Distributed Lock

Implement `IDistributedJobLock` for your environment.

**Example: MySQL-based Locking**

```csharp
using BackgroundJobManager.Abstractions;
using Dapper;
using MySqlConnector;

public class MySqlDistributedJobLock : IDistributedJobLock
{
	private readonly string _connectionString;

	public MySqlDistributedJobLock(string connectionString)
	{
		_connectionString = connectionString;
	}

	public async Task<bool> AcquireLockAsync(
		string jobId, 
		string instanceId, 
		TimeSpan lockDuration, 
		CancellationToken cancellationToken = default)
	{
		await using var connection = new MySqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var expiresAt = DateTime.UtcNow.Add(lockDuration);

		// Attempt to insert a lock record
		var sql = @"
			INSERT INTO JobLocks (JobId, InstanceId, ExpiresAt)
			VALUES (@JobId, @InstanceId, @ExpiresAt)
			ON DUPLICATE KEY UPDATE
				InstanceId = IF(ExpiresAt < @Now, @InstanceId, InstanceId),
				ExpiresAt = IF(ExpiresAt < @Now, @ExpiresAt, ExpiresAt)";

		await connection.ExecuteAsync(sql, new
		{
			JobId = jobId,
			InstanceId = instanceId,
			ExpiresAt = expiresAt,
			Now = DateTime.UtcNow
		});

		// Check if we successfully acquired the lock
		var lockOwner = await connection.QuerySingleOrDefaultAsync<string>(
			"SELECT InstanceId FROM JobLocks WHERE JobId = @JobId AND ExpiresAt > @Now",
			new { JobId = jobId, Now = DateTime.UtcNow });

		return lockOwner == instanceId;
	}

	public async Task ReleaseLockAsync(
		string jobId, 
		string instanceId, 
		CancellationToken cancellationToken = default)
	{
		await using var connection = new MySqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await connection.ExecuteAsync(
			"DELETE FROM JobLocks WHERE JobId = @JobId AND InstanceId = @InstanceId",
			new { JobId = jobId, InstanceId = instanceId });
	}

	public async Task<bool> RenewLockAsync(
		string jobId, 
		string instanceId, 
		TimeSpan lockDuration, 
		CancellationToken cancellationToken = default)
	{
		await using var connection = new MySqlConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		var expiresAt = DateTime.UtcNow.Add(lockDuration);

		var rowsAffected = await connection.ExecuteAsync(
			"UPDATE JobLocks SET ExpiresAt = @ExpiresAt WHERE JobId = @JobId AND InstanceId = @InstanceId",
			new { JobId = jobId, InstanceId = instanceId, ExpiresAt = expiresAt });

		return rowsAffected > 0;
	}
}
```

### 4. Configure Dependency Injection

Register all services in your `Program.cs`:

```csharp
using BackgroundJobManager.Core;

var builder = WebApplication.CreateBuilder(args);

// Add job management system
builder.Services.AddJobManagement(options =>
{
	options.PollingIntervalSeconds = 30;
	options.DefaultHistoryRetentionDays = 30;
	options.JobExecutionTimeoutMinutes = 60;
	options.LockDuration = TimeSpan.FromMinutes(5);
	options.InstanceId = Environment.MachineName;
});

// Register your repository implementations
builder.Services.AddScoped<IJobConfigurationRepository, JobConfigurationRepository>();
builder.Services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();
builder.Services.AddSingleton<IDistributedJobLock, MySqlDistributedJobLock>();

// Register your jobs
builder.Services.AddJob<SendEmailReportJob>();
builder.Services.AddJob<DataCleanupJob>();

// Or scan assemblies for jobs
// builder.Services.AddJobsFromAssemblies(ServiceLifetime.Scoped, typeof(Program).Assembly);

var app = builder.Build();

// Seed initial job configurations (one-time setup)
using (var scope = app.Services.CreateScope())
{
	var configRepo = scope.ServiceProvider.GetRequiredService<IJobConfigurationRepository>();

	await configRepo.UpsertAsync(new JobConfiguration
	{
		Id = "send-email-report",
		JobTypeName = typeof(SendEmailReportJob).AssemblyQualifiedName!,
		DisplayName = "Daily Email Report",
		CronSchedule = "0 0 8 * * *", // Every day at 8:00 AM
		Enabled = true,
		TimeZoneId = "America/New_York",
		RetentionDays = 90
	});
}

app.Run();
```

### 5. Use ISwitchboard to Manage Jobs

Inject `ISwitchboard` to interact with the job management system:

```csharp
[ApiController]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
	private readonly ISwitchboard _switchboard;

	public JobsController(ISwitchboard switchboard)
	{
		_switchboard = switchboard;
	}

	[HttpGet]
	public async Task<IActionResult> GetAllJobs()
	{
		var jobs = await _switchboard.GetJobConfigurationsAsync();
		return Ok(jobs);
	}

	[HttpGet("{jobId}")]
	public async Task<IActionResult> GetJob(string jobId)
	{
		var job = await _switchboard.GetJobConfigurationAsync(jobId);
		return job != null ? Ok(job) : NotFound();
	}

	[HttpPut("{jobId}/enable")]
	public async Task<IActionResult> EnableJob(string jobId)
	{
		var config = await _switchboard.GetJobConfigurationAsync(jobId);
		if (config == null) return NotFound();

		config.Enabled = true;
		await _switchboard.UpdateJobConfigurationAsync(config);

		return Ok();
	}

	[HttpPut("{jobId}/disable")]
	public async Task<IActionResult> DisableJob(string jobId)
	{
		var config = await _switchboard.GetJobConfigurationAsync(jobId);
		if (config == null) return NotFound();

		config.Enabled = false;
		await _switchboard.UpdateJobConfigurationAsync(config);

		return Ok();
	}

	[HttpPost("{jobId}/trigger")]
	public async Task<IActionResult> TriggerJob(string jobId)
	{
		await _switchboard.TriggerJobNowAsync(jobId);
		return Accepted();
	}

	[HttpGet("{jobId}/status")]
	public async Task<IActionResult> GetJobStatus(string jobId)
	{
		var status = await _switchboard.GetJobStatusAsync(jobId);
		return status != null ? Ok(status) : NotFound();
	}

	[HttpGet("{jobId}/history")]
	public async Task<IActionResult> GetJobHistory(string jobId, [FromQuery] int pageSize = 20, [FromQuery] int pageNumber = 1)
	{
		var history = await _switchboard.GetJobExecutionHistoryAsync(jobId, pageSize, pageNumber);
		return Ok(history);
	}

	[HttpGet("{jobId}/next-run")]
	public async Task<IActionResult> GetNextRun(string jobId)
	{
		var nextRun = await _switchboard.GetNextScheduledTimeAsync(jobId);
		return nextRun.HasValue ? Ok(new { nextRun = nextRun.Value }) : NotFound();
	}

	[HttpPut("{jobId}/schedule")]
	public async Task<IActionResult> UpdateSchedule(string jobId, [FromBody] UpdateScheduleRequest request)
	{
		var config = await _switchboard.GetJobConfigurationAsync(jobId);
		if (config == null) return NotFound();

		config.CronSchedule = request.CronSchedule;
		config.TimeZoneId = request.TimeZoneId ?? config.TimeZoneId;

		try
		{
			await _switchboard.UpdateJobConfigurationAsync(config);
			return Ok();
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new { error = ex.Message });
		}
	}
}

public record UpdateScheduleRequest(string CronSchedule, string? TimeZoneId);
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
