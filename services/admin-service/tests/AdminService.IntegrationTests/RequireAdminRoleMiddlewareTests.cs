using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AdminService.IntegrationTests;

/// <summary>
/// 中间件在 EF Core 之前拦截请求，401/403 场景不会触达数据库，
/// 因此这里只提供占位配置满足 AddApplicationServices/AddInfrastructureServices 的启动校验，无需真实依赖。
/// Program.cs 用的是 minimal hosting（WebApplication.CreateBuilder），
/// WebApplicationFactory 的 ConfigureAppConfiguration 钩子对这种启动方式不生效
/// （它是为通用 Host/IHostBuilder 设计的），所以改用环境变量注入——
/// CreateBuilder 内部的默认配置源本就包含环境变量，这条路径可靠。
/// </summary>
public class RequireAdminRoleMiddlewareTests : IClassFixture<WebApplicationFactory<Api.Program>>, IDisposable
{
    private static readonly Dictionary<string, string> _envOverrides = new()
    {
        ["ConnectionStrings__MySql"] = "Server=localhost;Port=3306;Database=admin_db_test;User=root;Password=root;",
        ["Services__SsoService__BaseUrl"] = "http://sso-service.default.svc.cluster.local",
        ["Services__JobService__BaseUrl"] = "http://backend-job-service.default.svc.cluster.local",
        ["TenantDatabase__Host"] = "localhost",
        ["TenantDatabase__Port"] = "3306",
        ["Internal__Token"] = "test-internal-token",
        ["DbInstanceEncryptionKey"] = "r9lrkFdjjSI03KYHue3SNf5M7EjtUStXSOvxZrLdDHI=",
    };

    private readonly WebApplicationFactory<Api.Program> _factory;

    public RequireAdminRoleMiddlewareTests(WebApplicationFactory<Api.Program> factory)
    {
        foreach (var (key, value) in _envOverrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        _factory = factory;
    }

    public void Dispose()
    {
        foreach (var key in _envOverrides.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public async Task Health_DoesNotRequireAdminRole()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MissingGatewayHeaders_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin-service/api/v1/settings");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NonAdminRole_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "1");
        client.DefaultRequestHeaders.Add("X-Username", "alice");
        client.DefaultRequestHeaders.Add("X-User-Roles", "default");

        var response = await client.GetAsync("/admin-service/api/v1/settings");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
