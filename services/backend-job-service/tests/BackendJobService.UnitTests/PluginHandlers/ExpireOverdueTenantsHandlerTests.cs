using System.Net;
using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Admin;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class ExpireOverdueTenantsHandlerTests
{
    private static TaskExecutionContext Context() => new() { ParametersJson = "{}" };

    private static ExpireOverdueTenantsHandler Sut(Func<HttpClient>? clientFactory = null) =>
        new(clientFactory ?? (() => throw new InvalidOperationException("测试用例不应该走到调用 admin-service 这一步")));

    [Fact]
    public async Task Execute_Success_ReturnsOkWithResponseBodyAsOutput()
    {
        var client = StubHttpMessageHandler.CreateClient(request =>
        {
            request.Method.ShouldBe(HttpMethod.Put);
            request.RequestUri!.AbsolutePath.ShouldBe("/internal/tenants/expire-overdue");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"expiredCount": 3}"""),
            };
        });

        var result = await Sut(() => client).ExecuteAsync(Context(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.OutputJson.ShouldNotBeNull().ShouldContain("expiredCount");
    }

    [Fact]
    public async Task Execute_AdminServiceCallFails_Fails()
    {
        var client = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await Sut(() => client).ExecuteAsync(Context(), CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_AdminServiceBaseUrlNotConfigured_FailsWithEnvVarName()
    {
        var result = await new ExpireOverdueTenantsHandler(
                () => throw new InvalidOperationException($"未配置环境变量 {MySqlPluginHelper.AdminServiceBaseUrlEnvVar}"))
            .ExecuteAsync(Context(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminServiceBaseUrlEnvVar);
    }

    [Fact]
    public void Handler_HasTaskPluginMetadata()
    {
        var metadata = typeof(ExpireOverdueTenantsHandler).GetCustomAttribute<TaskPluginAttribute>();

        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("admin-expire-overdue-tenants");
        metadata.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
