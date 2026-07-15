using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ITenantQueryService, TenantQueryService>();
        services.AddScoped<ITenantInternalService, TenantInternalService>();
        services.AddScoped<IDatabaseInstanceService, DatabaseInstanceService>();

        return services;
    }
}
