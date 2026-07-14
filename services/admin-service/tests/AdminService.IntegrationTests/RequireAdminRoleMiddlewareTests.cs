using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace AdminService.IntegrationTests;

/// <summary>
/// 中间件在 EF Core 之前拦截请求，401/403 场景不会触达数据库，
/// 因此这里只提供一个占位连接串满足 AddInfrastructureServices 的启动校验，无需真实 MySQL。
/// </summary>
public class RequireAdminRoleMiddlewareTests : IClassFixture<WebApplicationFactory<Api.Program>>
{
    private readonly WebApplicationFactory<Api.Program> _factory;

    public RequireAdminRoleMiddlewareTests(WebApplicationFactory<Api.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MySql"] = "Server=localhost;Port=3306;Database=admin_db_test;User=root;Password=root;",
                });
            });
        });
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
