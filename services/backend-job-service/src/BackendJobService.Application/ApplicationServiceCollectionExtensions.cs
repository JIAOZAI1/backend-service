using BackendJobService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BackendJobService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IExecutionQueryService, ExecutionQueryService>();
        return services;
    }
}
