# Background Job Manager

A lightweight background job management system for .NET 10 that provides a clean "switchboard" pattern for coordinating background jobs with simple abstractions.

## Features

- ✅ **Simple Base Class** - Extend `SwitchboardBackgroundService` to create background jobs
- ✅ **Clean Abstractions** - Minimal interfaces you implement to control job scheduling and execution
- ✅ **Structured Logging** - Automatic correlation IDs (RunId) for tracking individual executions
- ✅ **Anti-Concurrent** - Built-in jitter and status checks prevent overlapping job executions
- ✅ **Flexible** - Bring your own database, scheduler, and configuration approach

## Architecture

This library provides core abstractions only - you implement the persistence and scheduling logic:

### Core Components

- `ISwitchboard` - Interface for managing job state and execution logging (you implement this)
- `ISwitchboardUI` - Interface for UI operations like enable/disable, run now (you implement this)
- `SwitchboardBackgroundService` - Abstract base class for implementing background jobs
- Supporting types: `ExecuteResult`, `ExecuteResultType`, `NextRunInfo`, `LastRunInfo`, `ServiceStatus`


## Quick Start

### 1. Install Package

Add the package reference to your ASP.NET Core or console application:

```xml
<ItemGroup>
  <ProjectReference Include="..\BackgroundJobManager\Abstractions\Abstractions.csproj" />
</ItemGroup>
```

Or install via NuGet (when published):
```bash
dotnet add package BackgroundJobManager.Abstractions
```

### 2. Implement ISwitchboard

Create your own implementation to handle job scheduling and state:

```csharp
public class MySwitchboard : ISwitchboard
{
	public async Task<string> LogStartAsync(string serviceType, string machineName)
	{
		// Generate a unique run ID and mark the job as Running
		var runId = Guid.NewGuid().ToString();
		// Store in your database that this job started
		return runId;
	}

	public DateTime NextRunDateTimeUtc(string serviceType)
	{
		// Calculate when this job should run next based on your schedule logic
		return DateTime.UtcNow.AddMinutes(5);
	}

	public async Task LogResultAsync(string runId, string serviceType, LastRunInfo info, DateTime nextRun)
	{
		// Save the job execution result to your database
	}

	public async Task<NextRunInfo?> GetNextRunAsync(string serviceType)
	{
		// Return status (Scheduled, Disabled, or Running) and next scheduled time
		return new NextRunInfo(ServiceStatus.Scheduled, DateTime.UtcNow);
	}

	public async Task<LastRunInfo?> GetResultsAsync(string serviceType)
	{
		// Return the last execution result from your database
		return null;
	}
}
```

### 3. Create a Background Job

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

### 4. Register Services

In your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register your switchboard implementation
builder.Services.AddScoped<ISwitchboard, MySwitchboard>();

// Optionally register the UI interface for admin operations
builder.Services.AddScoped<ISwitchboardUI, MySwitchboardUI>();

// Register your background jobs as hosted services
builder.Services.AddHostedService<EmailReportJob>();

var app = builder.Build();
app.Run();
```


## How It Works

### Execution Flow

1. **Job Registration**: When you add a `HostedService` that extends `SwitchboardBackgroundService`, it starts running
2. **Scheduling Loop**: Each job continuously loops with jitter delays (10-15 seconds by default)
3. **Schedule Check**: Before each execution, the job queries `ISwitchboard.GetNextRunAsync()` to check:
   - Is the job enabled? (ServiceStatus.Scheduled)
   - Is it already running? (ServiceStatus.Running)
   - Should it run now based on when it's scheduled?
4. **Execution**: If checks pass:
   - `LogStartAsync()` creates a run record and generates a RunId (correlation ID)
   - Your `ExecuteInternalAsync()` method runs
   - `LogResultAsync()` records success/failure, duration, and any exceptions
5. **Next Run Calculation**: Your switchboard calculates the next scheduled run via `NextRunDateTimeUtc()`

### Anti-Concurrent Protection

Jobs prevent overlapping executions through:
- **Status tracking**: Jobs marked as "Running" won't start again
- **Jitter delays**: Random delays (10-15 seconds) between loop iterations prevent clock-synchronized races
- **Schedule adherence**: Jobs respect the schedule you provide via `GetNextRunAsync()`

### Customizable Delays

Override these properties in your job class to adjust timing:

```csharp
protected override int MinLoopDelaySeconds => 30;  // Minimum delay between checks
protected override int MaxLoopDelaySeconds => 60;  // Maximum delay between checks
```


## Interfaces

### ISwitchboard

The core interface that coordinates job execution. You must implement this:

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
}
```

**Method Responsibilities:**
- `LogStartAsync()` - Generate a unique RunId, mark job as Running, return the RunId
- `LogResultAsync()` - Save execution results (duration, success/failure, exceptions)
- `NextRunDateTimeUtc()` - Calculate when the job should run next
- `GetNextRunAsync()` - Return job status and next scheduled time
- `GetResultsAsync()` - Return the last execution information

### ISwitchboardUI

Optional interface for UI/admin operations:

```csharp
public interface ISwitchboardUI
{
    Task<IDictionary<string, (NextRunInfo? NextRun, LastRunInfo? LastRun)>> GetAdminViewAsync();
    Task EnableAsync(string serviceType);
    Task DisableAsync(string serviceType);
    Task RunNowAsync(string serviceType);
}
```

**Method Responsibilities:**
- `GetAdminViewAsync()` - Get overview of all jobs and their statuses
- `EnableAsync()` - Mark a job as enabled (ServiceStatus.Scheduled)
- `DisableAsync()` - Mark a job as disabled (ServiceStatus.Disabled)
- `RunNowAsync()` - Schedule a job to run immediately (set next run to now)

### Example: Admin Dashboard

```csharp
[ApiController]
[Route("api/jobs")]
public class JobsAdminController : ControllerBase
{
    private readonly ISwitchboard _switchboard;
    private readonly ISwitchboardUI _switchboardUI;

    public JobsAdminController(ISwitchboard switchboard, ISwitchboardUI switchboardUI)
    {
        _switchboard = switchboard;
        _switchboardUI = switchboardUI;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        var overview = await _switchboardUI.GetAdminViewAsync();
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
        await _switchboardUI.EnableAsync(serviceName);
        return Ok();
    }

    [HttpPut("{serviceName}/disable")]
    public async Task<IActionResult> DisableJob(string serviceName)
    {
        await _switchboardUI.DisableAsync(serviceName);
        return Ok();
    }

    [HttpPost("{serviceName}/run-now")]
    public async Task<IActionResult> RunNow(string serviceName)
    {
        await _switchboardUI.RunNowAsync(serviceName);
        return Ok();
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

### Testing Your ISwitchboard Implementation

Test your switchboard with appropriate mocking or in-memory storage:

```csharp
[Fact]
public async Task Switchboard_TracksJobExecution()
{
    // Arrange
    var switchboard = new YourSwitchboardImplementation(/* dependencies */);

    // Act
    var runId = await switchboard.LogStartAsync("TestJob", "TestMachine");
    var info = new LastRunInfo(
        DateTime.UtcNow,
        DateTime.UtcNow.AddSeconds(5),
        TimeSpan.FromSeconds(5),
        new ExecuteResult(true, "Success"));
    await switchboard.LogResultAsync(runId, "TestJob", info, DateTime.UtcNow.AddMinutes(5));

    // Assert
    Assert.NotNull(runId);
    var lastRun = await switchboard.GetResultsAsync("TestJob");
    Assert.NotNull(lastRun);
    Assert.True(lastRun.Result.Success);
}
```

## Best Practices

1. **Idempotency** - Design jobs to be safely re-executable in case of failures
2. **Error Handling** - Jobs should catch and log exceptions; the framework will capture them anyway
3. **Clock Sync** - Ensure NTP is configured in distributed environments for accurate scheduling
4. **Testing** - Mock `ISwitchboard` for unit tests, test your implementation separately
5. **Structured Logging** - Use the RunId in your logs for correlation across distributed traces

## Implementation Examples

### Simple In-Memory Switchboard

For development or simple scenarios:

```csharp
public class InMemorySwitchboard : ISwitchboard
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();

    public Task<string> LogStartAsync(string serviceType, string machineName)
    {
        var runId = Guid.NewGuid().ToString();
        _jobs.AddOrUpdate(serviceType, 
            new JobState { Status = ServiceStatus.Running, CurrentRunId = runId },
            (_, state) => state with { Status = ServiceStatus.Running, CurrentRunId = runId });
        return Task.FromResult(runId);
    }

    public DateTime NextRunDateTimeUtc(string serviceType) => DateTime.UtcNow.AddMinutes(5);

    public Task LogResultAsync(string runId, string serviceType, LastRunInfo info, DateTime nextRun)
    {
        _jobs.AddOrUpdate(serviceType, 
            new JobState { Status = ServiceStatus.Scheduled, LastRun = info, NextRun = nextRun },
            (_, state) => state with { Status = ServiceStatus.Scheduled, LastRun = info, NextRun = nextRun });
        return Task.CompletedTask;
    }

    public Task<NextRunInfo?> GetNextRunAsync(string serviceType)
    {
        if (_jobs.TryGetValue(serviceType, out var state))
            return Task.FromResult<NextRunInfo?>(new NextRunInfo(state.Status, state.NextRun));
        return Task.FromResult<NextRunInfo?>(new NextRunInfo(ServiceStatus.Scheduled, DateTime.UtcNow));
    }

    public Task<LastRunInfo?> GetResultsAsync(string serviceType)
    {
        if (_jobs.TryGetValue(serviceType, out var state))
            return Task.FromResult(state.LastRun);
        return Task.FromResult<LastRunInfo?>(null);
    }

    private record JobState
    {
        public ServiceStatus Status { get; init; }
        public DateTime? NextRun { get; init; }
        public LastRunInfo? LastRun { get; init; }
        public string? CurrentRunId { get; init; }
    }
}
```

## License

MIT
