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
