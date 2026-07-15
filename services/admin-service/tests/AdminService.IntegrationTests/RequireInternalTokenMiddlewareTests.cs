using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AdminService.IntegrationTests;

/// <summary>
/// 校验 /internal/* 分支的鉴权行为：与 RequireAdminRoleMiddlewareTests 同样的启动方式说明，见该文件顶部注释。
/// 这里只覆盖鉴权层面的 401 场景（在触达 EF Core/仓储之前短路返回），不覆盖 200 场景——
/// 200 需要真实数据库里存在对应记录，属于更适合单元测试（TenantInternalServiceTests、
/// DatabaseInstanceService 相关）覆盖的范围。
/// </summary>
public class RequireInternalTokenMiddlewareTests : IClassFixture<WebApplicationFactory<Api.Program>>, IDisposable
{
    private static readonly Dictionary<string, string> _envOverrides = new()
    {
        ["ConnectionStrings__MySql"] = "Server=localhost;Port=3306;Database=admin_db_test;User=root;Password=root;",
        ["Services__SsoService__BaseUrl"] = "http://sso-service.default.svc.cluster.local",
        ["Services__JobService__BaseUrl"] = "http://backend-job-service.default.svc.cluster.local",
        ["Internal__Token"] = "test-internal-token",
        ["DbInstanceEncryptionKey"] = "r9lrkFdjjSI03KYHue3SNf5M7EjtUStXSOvxZrLdDHI=",
    };

    private readonly WebApplicationFactory<Api.Program> _factory;

    public RequireInternalTokenMiddlewareTests(WebApplicationFactory<Api.Program> factory)
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
    public async Task MissingInternalToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/internal/database-instances/1/credentials");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongInternalToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Token", "not-the-right-token");

        var response = await client.GetAsync("/internal/database-instances/1/credentials");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// 关键回归防护：只带网关登录身份头、不带内部密钥的请求，不能绕过 /internal/* 的鉴权。
    /// 这条路径专供集群内机器对机器调用，从设计上就不该信任网关身份——防止 Program.cs 的
    /// MapWhen 分流写错，把 RequireAdminRoleMiddleware 那种"网关身份即放行"的语义带进来。
    /// </summary>
    [Fact]
    public async Task GatewayIdentityHeadersWithoutInternalToken_StillReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "1");
        client.DefaultRequestHeaders.Add("X-Username", "alice");
        client.DefaultRequestHeaders.Add("X-User-Roles", "admin");

        var response = await client.PutAsync("/internal/tenants/some-tenant-id/activate", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
