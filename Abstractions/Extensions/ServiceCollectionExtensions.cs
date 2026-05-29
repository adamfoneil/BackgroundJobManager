using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ManagedBackgroundJob.Abstractions.Data;
using ManagedBackgroundJob.Abstractions.Services;
using Abstractions;

namespace ManagedBackgroundJob.Abstractions.Extensions;

/// <summary>
/// Extension methods for registering ISwitchboard services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ISwitchboard implementation with EF Core support
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureDb">Action to configure the DbContext (e.g., connection string)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddManagedBackgroundJobs(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
    {
        // Register DbContext
        services.AddDbContext<ManagedJobDbContext>(configureDb);

        // Register default cron evaluator (can be replaced with custom implementation)
        services.AddSingleton<ICronEvaluator, DefaultCronEvaluator>();

        // Register the switchboard
        services.AddScoped<ISwitchboard, SwitchboardService>();

        return services;
    }

    /// <summary>
    /// Adds the ISwitchboard implementation with EF Core support and custom cron evaluator
    /// </summary>
    /// <typeparam name="TCronEvaluator">Custom cron evaluator implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configureDb">Action to configure the DbContext (e.g., connection string)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddManagedBackgroundJobs<TCronEvaluator>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
        where TCronEvaluator : class, ICronEvaluator
    {
        // Register DbContext
        services.AddDbContext<ManagedJobDbContext>(configureDb);

        // Register custom cron evaluator
        services.AddSingleton<ICronEvaluator, TCronEvaluator>();

        // Register the switchboard
        services.AddScoped<ISwitchboard, SwitchboardService>();

        return services;
    }
}
