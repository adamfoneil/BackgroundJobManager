# ISwitchboard Implementation

This implementation provides a complete ISwitchboard service using Entity Framework Core for persistence.

## Components

### Entities (`Entities/`)
- **JobConfiguration**: Stores job settings (enabled/disabled, cron schedule, timezone)
- **JobRun**: Tracks individual execution runs with start/finish times and results

### Data (`Data/`)
- **ManagedJobDbContext**: EF Core DbContext with proper indexing for performance

### Services (`Services/`)
- **SwitchboardService**: Main implementation of ISwitchboard interface
- **DefaultCronEvaluator**: Simple cron evaluator (supports interval notation like "5m", "1h")

### Scheduling (`Scheduling/`)
- **ICronEvaluator**: Interface for cron schedule evaluation

## Usage

### 1. Register Services

```csharp
// In Program.cs or Startup.cs
services.AddManagedBackgroundJobs(options =>
{
	options.UseSqlServer(connectionString);
	// or options.UseSqlite(connectionString);
	// or options.UseNpgsql(connectionString);
});
```

### 2. Run Migrations

```bash
# Add migration
dotnet ef migrations add InitialCreate --project YourProject

# Update database
dotnet ef database update --project YourProject
```

### 3. Create Your Background Service

```csharp
public class MyBackgroundService : ManagedBackgroundService
{
	public MyBackgroundService(
		ILogger<MyBackgroundService> logger,
		ISwitchboard switchboard)
		: base(logger, switchboard)
	{
	}

	protected override async Task<ExecuteResult> ExecuteInternalAsync(
		string runId, 
		CancellationToken stoppingToken)
	{
		try
		{
			// Your work here
			await Task.Delay(1000, stoppingToken);

			return new ExecuteResult(true, "Work completed successfully");
		}
		catch (Exception ex)
		{
			return new ExecuteResult(false, "Work failed", ex);
		}
	}
}
```

### 4. Register Your Background Service

```csharp
services.AddHostedService<MyBackgroundService>();
```

## Configuration Examples

### Set Cron Schedule via Database

```sql
-- Run every 5 minutes
UPDATE JobConfigurations 
SET CronSchedule = '5m'
WHERE ServiceName = 'MyBackgroundService';

-- Run every hour
UPDATE JobConfigurations 
SET CronSchedule = '1h'
WHERE ServiceName = 'MyBackgroundService';

-- Disable a job
UPDATE JobConfigurations 
SET IsEnabled = 0
WHERE ServiceName = 'MyBackgroundService';
```

### Programmatic Configuration

```csharp
// Inject ISwitchboard and configure
public void ConfigureJobs(ISwitchboard switchboard)
{
	switchboard.Enable("MyBackgroundService");
	switchboard.Disable("OtherService");
}
```

## Upgrading to a Real Cron Library

The default `DefaultCronEvaluator` only supports simple interval notation ("5m", "1h", etc.).

For production use with proper cron expressions, replace it with a library like **Cronos**:

### 1. Install Cronos

```bash
dotnet add package Cronos
```

### 2. Create Custom Evaluator

```csharp
using Cronos;

public class CronosEvaluator : ICronEvaluator
{
	public DateTimeOffset? GetNextOccurrence(
		string cronExpression, 
		DateTimeOffset from, 
		string timeZoneId)
	{
		var expression = CronExpression.Parse(cronExpression);
		var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		return expression.GetNextOccurrence(from, timezone);
	}

	public bool IsValid(string cronExpression)
	{
		return CronExpression.TryParse(cronExpression, out _);
	}
}
```

### 3. Register Custom Evaluator

```csharp
services.AddManagedBackgroundJobs<CronosEvaluator>(options =>
{
	options.UseSqlServer(connectionString);
});
```

## Database Schema

### JobConfigurations Table
- ServiceName (PK, varchar(200))
- IsEnabled (bit)
- CronSchedule (varchar(100), nullable)
- TimeZoneId (varchar(100))
- LastScheduleCheck (datetimeoffset, nullable)
- NextScheduledRun (datetimeoffset, nullable)

### JobRuns Table
- RunId (PK, varchar(50))
- ServiceName (varchar(200))
- StartedAt (datetimeoffset)
- FinishedAt (datetimeoffset, nullable)
- Success (bit, nullable)
- Message (varchar(2000), nullable)
- ExceptionDetails (nvarchar(max), nullable)

## Features

- ✅ Enable/disable jobs dynamically
- ✅ Cron-based scheduling
- ✅ Continuous execution mode (no cron schedule)
- ✅ Prevents concurrent runs of the same job
- ✅ Tracks execution history with results
- ✅ Timezone support
- ✅ Exception logging
- ✅ Structured logging with RunId scope
- ✅ EF Core agnostic (SQL Server, SQLite, PostgreSQL, etc.)
