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


## How It Works

### Execution Flow

1. **Job Registration**: When you add a `HostedService` that extends `SwitchboardBackgroundService`, it registers automatically
2. **Configuration**: On first run, the job creates a `JobConfiguration` entry in the database (enabled by default, no schedule)
3. **Scheduling Loop**: Each job continuously loops with jitter delays (10-15 seconds by default)
4. **Schedule Check**: Before each execution, the job queries `ISwitchboard.GetNextRunAsync()` to check:
   - Is the job enabled?
   - Is it already running?
   - Should it run now based on the schedule?
5. **Execution**: If checks pass:
   - `LogStartAsync()` creates a `JobRun` record and generates a RunId
   - Your `ExecuteInternalAsync()` method runs
   - `LogResultAsync()` records success/failure, duration, and any exceptions
6. **Next Run Calculation**: The next scheduled run is calculated and stored

### Anti-Concurrent Protection

Jobs prevent overlapping executions through:
- **Status tracking**: Jobs marked as "Running" won't start again
- **Jitter delays**: Random delays (10-15 seconds) between loop iterations prevent clock-synchronized races
- **Schedule adherence**: Jobs respect their `NextScheduledRun` timestamp

### Customizable Delays

Override these properties in your job class to adjust timing:

```csharp
protected override int MinLoopDelaySeconds => 30;  // Minimum delay between checks
protected override int MaxLoopDelaySeconds => 60;  // Maximum delay between checks
```

## Schedule Notation

The default `DefaultCronEvaluator` supports simple interval notation:

| Notation | Interval |
|----------|----------|
| `5s` | Every 5 seconds |
| `30s` | Every 30 seconds |
| `5m` | Every 5 minutes |
| `1h` | Every hour |
| `12h` | Every 12 hours |

**No schedule** (null or empty `CronSchedule`) means the job runs continuously (respects jitter delays only).

### Upgrading to Full Cron Support

To use standard cron expressions like `"0 8 * * MON"` (every Monday at 8 AM), implement a custom `ICronEvaluator`:

```csharp
// Install Cronos: dotnet add package Cronos
using Cronos;

public class CronosEvaluator : ICronEvaluator
{
    public DateTimeOffset? GetNextOccurrence(
        string cronExpression, 
        DateTimeOffset from, 
        string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        var expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return expression.GetNextOccurrence(from, timezone);
    }

    public bool IsValid(string cronExpression)
    {
        return CronExpression.TryParse(cronExpression, CronFormat.IncludeSeconds, out _);
    }
}
```

Then register it:

```csharp
builder.Services.AddSingleton<ICronEvaluator, CronosEvaluator>();
```


## Database Schema

The system uses two main tables:

### JobConfiguration
Stores job settings and schedule configuration.

| Column | Type | Description |
|--------|------|-------------|
| `ServiceName` | varchar(200) PK | Unique identifier for the job (matches your class name) |
| `IsEnabled` | bit | Whether the job can run |
| `CronSchedule` | varchar(100) | Schedule notation (null/empty = continuous) |
| `TimeZoneId` | varchar(100) | Timezone for schedule evaluation (default: UTC) |
| `LastScheduleCheck` | datetimeoffset | Last time schedule was evaluated |
| `NextScheduledRun` | datetimeoffset | Calculated next run time |

**Indexes:**
- `IsEnabled`
- `NextScheduledRun`

### JobRun
Tracks individual job executions.

| Column | Type | Description |
|--------|------|-------------|
| `RunId` | varchar(50) PK | Unique execution identifier |
| `ServiceName` | varchar(200) | Which job executed |
| `StartedAt` | datetimeoffset | Execution start time |
| `FinishedAt` | datetimeoffset | Execution end time (nullable) |
| `Success` | bit | Whether execution succeeded (nullable while running) |
| `Message` | varchar(2000) | Result message |
| `ExceptionDetails` | nvarchar(max) | Full exception details if failed |

**Indexes:**
- `ServiceName`
- `StartedAt`
- `(ServiceName, StartedAt)` composite
- `FinishedAt`

## Using ISwitchboard

The `ISwitchboard` interface provides methods for runtime interaction with jobs. It's automatically used by `SwitchboardBackgroundService`, but you can also inject it for custom scenarios:

### Core Methods

```csharp
public interface ISwitchboard
{
    // Called automatically by SwitchboardBackgroundService
    Task<string> LogStartAsync(string serviceType, string machineName);
    Task LogResultAsync(string runId, string serviceType, LastRunInfo info, DateTime nextRun);
    DateTime NextRunDateTimeUtc(string serviceType);

    // For querying status
    Task<NextRunInfo?> GetNextRunAsync(string serviceType);
    Task<LastRunInfo?> GetResultsAsync(string serviceType);
    Task<IDictionary<string, (NextRunInfo? NextRun, LastRunInfo? LastRun)>> GetAdminViewAsync();
}
```

### Example: Admin Dashboard

```csharp
[ApiController]
[Route("api/jobs")]
public class JobsAdminController : ControllerBase
{
    private readonly ISwitchboard _switchboard;
    private readonly ManagedJobDbContext _dbContext;

    public JobsAdminController(ISwitchboard switchboard, ManagedJobDbContext dbContext)
    {
        _switchboard = switchboard;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        var overview = await _switchboard.GetAdminViewAsync();
        return Ok(overview);
    }

    [HttpGet("{serviceName}/status")]
    public async Task<IActionResult> GetJobStatus(string serviceName)
    {
        var nextRun = await _switchboard.GetNextRunAsync(serviceName);
        var lastRun = await _switchboard.GetResultsAsync(serviceName);

        return Ok(new { nextRun, lastRun });
    }

    [HttpPut("{serviceName}/enable")]
    public async Task<IActionResult> EnableJob(string serviceName)
    {
        var config = await _dbContext.JobConfigurations.FindAsync(serviceName);
        if (config == null) return NotFound();

        config.IsEnabled = true;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{serviceName}/disable")]
    public async Task<IActionResult> DisableJob(string serviceName)
    {
        var config = await _dbContext.JobConfigurations.FindAsync(serviceName);
        if (config == null) return NotFound();

        config.IsEnabled = false;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{serviceName}/schedule")]
    public async Task<IActionResult> UpdateSchedule(
        string serviceName, 
        [FromBody] string cronSchedule)
    {
        var config = await _dbContext.JobConfigurations.FindAsync(serviceName);
        if (config == null) return NotFound();

        config.CronSchedule = cronSchedule;
        config.NextScheduledRun = null; // Force recalculation
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("{serviceName}/history")]
    public async Task<IActionResult> GetHistory(
        string serviceName,
        [FromQuery] int limit = 50)
    {
        var history = await _dbContext.JobRuns
            .Where(r => r.ServiceName == serviceName)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(history);
    }
}
```


## Advanced Scenarios

### Custom Service Type Name

By default, the job uses the class name as `ServiceType`. Override to customize:

```csharp
protected override string ServiceType => "my-custom-job-name";
```

### Adjusting Loop Delays

Control how frequently the job checks if it should run:

```csharp
protected override int MinLoopDelaySeconds => 5;   // Check every 5-10 seconds
protected override int MaxLoopDelaySeconds => 10;
```

### Collecting Custom Messages

Return additional context with your execution results:

```csharp
protected override async Task<ExecuteResult> ExecuteInternalAsync(
    string runId, 
    CancellationToken stoppingToken)
{
    var messages = new List<string>();

    if (someCondition)
        messages.Add("Warning: Low disk space");

    if (anotherCondition)
        messages.Add("Skipped 5 invalid records");

    return new ExecuteResult(true, "Processed 100 records")
    {
        Messages = messages.ToArray()
    };
}
```

### Machine Name Tracking

Each job execution automatically logs `Environment.MachineName` via `LogStartAsync()`, useful for identifying which instance ran the job in distributed environments.

### Manual Database Configuration

For more control over job configuration during startup:

```csharp
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ManagedJobDbContext>();

    // Ensure database exists
    await dbContext.Database.MigrateAsync();

    // Pre-configure a job
    var config = await dbContext.JobConfigurations.FindAsync("EmailReportJob");
    if (config == null)
    {
        dbContext.JobConfigurations.Add(new JobConfiguration
        {
            ServiceName = "EmailReportJob",
            IsEnabled = true,
            CronSchedule = "1h",
            TimeZoneId = "America/New_York"
        });
        await dbContext.SaveChangesAsync();
    }
}
```

## Testing

### Unit Testing Your Job

Mock the dependencies to test your job logic:

```csharp
[Fact]
public async Task EmailReportJob_SendsReport_Successfully()
{
    // Arrange
    var logger = new Mock<ILogger<EmailReportJob>>();
    var switchboard = new Mock<ISwitchboard>();
    var emailService = new Mock<IEmailService>();

    var job = new EmailReportJob(logger.Object, switchboard.Object, emailService.Object);

    // Act
    var result = await job.ExecuteInternalAsync("test-run-123", CancellationToken.None);

    // Assert
    Assert.True(result.Success);
    emailService.Verify(e => e.SendAsync(
        It.IsAny<string>(), 
        It.IsAny<string>(), 
        It.IsAny<string>()), 
        Times.Once);
}
```

### Integration Testing

Use an in-memory database for integration tests:

```csharp
var options = new DbContextOptionsBuilder<ManagedJobDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;

using var context = new ManagedJobDbContext(options);
var switchboard = new SwitchboardService(context, new DefaultCronEvaluator(), logger);

// Test switchboard operations
var runId = await switchboard.LogStartAsync("TestJob", "TestMachine");
Assert.NotNull(runId);
```

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
