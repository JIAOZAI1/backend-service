using BackendJobService.Application.Interfaces;
using BackendJobService.Infrastructure.Messaging;
using BackendJobService.Infrastructure.Persistence;
using BackendJobService.Infrastructure.Plugins;
using BackendJobService.Infrastructure.Repositories;
using BackendJobService.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BackendJobService.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mysqlConnectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("ConnectionStrings:MySql is not configured");

        services.AddDbContext<JobDbContext>(options =>
            options.UseMySql(mysqlConnectionString, ServerVersion.AutoDetect(mysqlConnectionString)));

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddScoped<ITaskExecutionPublisher, RabbitMqTaskExecutionPublisher>();

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobExecutionRepository, JobExecutionRepository>();

        var pluginDirectory = configuration["Plugins:Directory"]
            ?? throw new InvalidOperationException("Plugins:Directory is not configured");
        services.AddSingleton<ITaskHandlerLoader>(sp =>
            new TaskHandlerLoader(pluginDirectory, sp.GetRequiredService<ILogger<TaskHandlerLoader>>()));

        services.AddHostedService<JobSchedulerHostedService>();
        services.AddHostedService<TaskWorkerHostedService>();

        return services;
    }
}
