using AdminService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        return services;
    }
}
