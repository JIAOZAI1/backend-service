using AdminService.Application.Interfaces;
using AdminService.Infrastructure.ExternalClients;
using AdminService.Infrastructure.Persistence;
using AdminService.Infrastructure.Repositories;
using AdminService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdminService.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mysqlConnectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("ConnectionStrings:MySql is not configured");

        services.AddDbContext<AdminDbContext>(options =>
            options.UseMySql(mysqlConnectionString, ServerVersion.AutoDetect(mysqlConnectionString)));

        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserTenantRepository, UserTenantRepository>();
        services.AddScoped<IDatabaseInstanceRepository, DatabaseInstanceRepository>();

        var dbInstanceEncryptionKey = configuration["DbInstanceEncryptionKey"]
            ?? throw new InvalidOperationException("DbInstanceEncryptionKey is not configured");
        services.AddSingleton<IDbCredentialCipher>(new AesGcmDbCredentialCipher(dbInstanceEncryptionKey));

        var internalToken = configuration["Internal:Token"]
            ?? throw new InvalidOperationException("Internal:Token is not configured");
        services.AddTransient(_ => new InternalTokenDelegatingHandler(internalToken));

        AddExternalServiceClient<ISsoServiceClient, SsoServiceClient>(services, configuration, "Services:SsoService:BaseUrl");
        AddExternalServiceClient<IJobServiceClient, JobServiceClient>(services, configuration, "Services:JobService:BaseUrl");

        return services;
    }

    private static void AddExternalServiceClient<TInterface, TImplementation>(
        IServiceCollection services, IConfiguration configuration, string baseUrlConfigKey)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        var baseUrl = configuration[baseUrlConfigKey]
            ?? throw new InvalidOperationException($"{baseUrlConfigKey} is not configured");

        services.AddHttpClient<TInterface, TImplementation>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        }).AddHttpMessageHandler<InternalTokenDelegatingHandler>();
    }
}
