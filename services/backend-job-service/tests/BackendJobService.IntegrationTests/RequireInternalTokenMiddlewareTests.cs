using BackendJobService.Api.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace BackendJobService.IntegrationTests;

/// <summary>
/// 直接用 TestServer 挂载中间件验证路由匹配与密钥校验逻辑，不经过完整的 Program 启动流程
/// （完整启动依赖真实 MySQL/RabbitMQ，见 RequireAdminRoleMiddlewareTests 在 admin-service 的先例）。
/// </summary>
public class RequireInternalTokenMiddlewareTests
{
    private const string ExpectedToken = "test-internal-token";

    private static async Task<(int StatusCode, HttpClient Client)> SendAsync(
        HttpMethod method, string path, string? token, bool withGatewayUser = false)
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<RequireInternalTokenMiddleware>(ExpectedToken);
                    app.Run(async context => await context.Response.WriteAsync("ok"));
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
        {
            request.Headers.Add(RequireInternalTokenMiddleware.InternalTokenHeader, token);
        }

        if (withGatewayUser)
        {
            request.Headers.Add(GatewayUser.UserIdHeader, "1");
            request.Headers.Add(GatewayUser.UsernameHeader, "alice");
        }

        var response = await client.SendAsync(request);
        return ((int)response.StatusCode, client);
    }

    [Fact]
    public async Task CreateJob_WithValidToken_PassesThrough()
    {
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs", ExpectedToken);
        statusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateJob_WithoutToken_ReturnsUnauthorized()
    {
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs", null);
        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task CreateJob_WithWrongToken_ReturnsUnauthorized()
    {
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs", "wrong-token");
        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task CreateTask_WithoutToken_ReturnsUnauthorized()
    {
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs/123/tasks", null);
        statusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task CreateJob_WithGatewayUser_PassesThroughWithoutToken()
    {
        // 前端经网关登录校验后调用，带 X-User-Id/X-Username，不需要内部密钥（规范 16.5.3：
        // 作业创建是公开业务 API，不能要求前端持有集群内部共享密钥）
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs", null, withGatewayUser: true);
        statusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateTask_WithGatewayUser_PassesThroughWithoutToken()
    {
        var (statusCode, _) = await SendAsync(HttpMethod.Post, "/backend-job-service/api/v1/jobs/123/tasks", null, withGatewayUser: true);
        statusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task ListJobs_WithoutToken_StillAllowed()
    {
        // GET /jobs 是只读接口，不在保护范围内，本次不加密钥校验
        var (statusCode, _) = await SendAsync(HttpMethod.Get, "/backend-job-service/api/v1/jobs", null);
        statusCode.ShouldBe(StatusCodes.Status200OK);
    }
}
