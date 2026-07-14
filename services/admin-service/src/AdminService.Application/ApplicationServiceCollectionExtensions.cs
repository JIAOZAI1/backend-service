using AdminService.Application.Common;
using AdminService.Application.Interfaces;
using AdminService.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ITenantQueryService, TenantQueryService>();

        services.AddSingleton(new TenantDatabaseOptions
        {
            DbType = configuration["TenantDatabase:Type"] ?? "mysql",
            Host = configuration["TenantDatabase:Host"]
                ?? throw new InvalidOperationException("TenantDatabase:Host is not configured"),
            Port = int.TryParse(configuration["TenantDatabase:Port"], out var port) ? port : 3306,
        });

        return services;
    }
}
