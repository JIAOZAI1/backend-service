using System.Net;
using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Admin;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class ActivateTenantHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    private static ActivateTenantHandler Sut(Func<HttpClient>? clientFactory = null) =>
        new(clientFactory ?? (() => throw new InvalidOperationException("测试用例不应该走到调用 admin-service 这一步")));

    [Fact]
    public async Task Execute_InvalidJson_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("not json"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("合法 JSON");
    }

    [Fact]
    public async Task Execute_MissingTenantId_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("{}"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("tenantId");
    }

    [Fact]
    public async Task Execute_TenantNotFound_Fails()
    {
        var client = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"tenantId": "d290f1ee-6c54-4b01-90e6-d701748f0851"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("d290f1ee-6c54-4b01-90e6-d701748f0851");
    }

    [Fact]
    public async Task Execute_Success_ReturnsOk()
    {
        var client = StubHttpMessageHandler.CreateClient(request =>
        {
            request.Method.ShouldBe(HttpMethod.Put);
            request.RequestUri!.AbsolutePath.ShouldBe("/internal/tenants/d290f1ee-6c54-4b01-90e6-d701748f0851/activate");
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"tenantId": "d290f1ee-6c54-4b01-90e6-d701748f0851"}"""), CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_AdminServiceBaseUrlNotConfigured_FailsWithEnvVarName()
    {
        var result = await new ActivateTenantHandler(() => throw new InvalidOperationException($"未配置环境变量 {MySqlPluginHelper.AdminServiceBaseUrlEnvVar}"))
            .ExecuteAsync(Context("""{"tenantId": "d290f1ee-6c54-4b01-90e6-d701748f0851"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminServiceBaseUrlEnvVar);
    }

    [Fact]
    public void Handler_HasTaskPluginMetadata()
    {
        var metadata = typeof(ActivateTenantHandler).GetCustomAttribute<TaskPluginAttribute>();

        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("admin-activate-tenant");
        metadata.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
