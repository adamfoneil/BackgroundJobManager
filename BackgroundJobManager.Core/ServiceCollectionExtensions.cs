using BackgroundJobManager.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BackgroundJobManager.Core;

/// <summary>
/// Extension methods for configuring job management services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the job management system to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Optional configuration action for JobManagementOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobManagement(
        this IServiceCollection services,
        Action<JobManagementOptions>? configureOptions = null)
    {
        // Configure options
        var options = new JobManagementOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // Register core services
        services.AddSingleton<ICronScheduleEvaluator, CronScheduleEvaluator>();
        services.AddScoped<ISwitchboard, Switchboard>();
        services.AddScoped<JobExecutionWrapper>();

        // Register the orchestration background service
        services.AddHostedService<JobOrchestrationService>();

        return services;
    }

    /// <summary>
    /// Registers a job implementation in the dependency injection container.
    /// Jobs must implement the IJob interface.
    /// </summary>
    /// <typeparam name="TJob">The job type to register.</typeparam>
    /// <param name="services">The service collection to add the job to.</param>
    /// <param name="lifetime">The service lifetime (default: Scoped).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJob<TJob>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TJob : class, IJob
    {
        services.Add(new ServiceDescriptor(typeof(TJob), typeof(TJob), lifetime));
        return services;
    }

    /// <summary>
    /// Registers multiple job implementations from an assembly.
    /// Scans for all types implementing IJob and registers them.
    /// </summary>
    /// <param name="services">The service collection to add jobs to.</param>
    /// <param name="assemblies">The assemblies to scan for job implementations.</param>
    /// <param name="lifetime">The service lifetime for discovered jobs (default: Scoped).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobsFromAssemblies(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params System.Reflection.Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = new[] { System.Reflection.Assembly.GetCallingAssembly() };
        }

        foreach (var assembly in assemblies)
        {
            var jobTypes = assembly.GetTypes()
                .Where(t => typeof(IJob).IsAssignableFrom(t)
                    && t.IsClass
                    && !t.IsAbstract
                    && t.IsPublic);

            foreach (var jobType in jobTypes)
            {
                services.Add(new ServiceDescriptor(jobType, jobType, lifetime));
            }
        }

        return services;
    }
}
