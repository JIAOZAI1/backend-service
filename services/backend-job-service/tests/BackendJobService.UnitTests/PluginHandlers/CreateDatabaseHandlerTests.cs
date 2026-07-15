using System.Net;
using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class CreateDatabaseHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    // 校验失败必须发生在调用 admin-service/连接数据库之前，因此这些用例不需要真实的 HttpClient/MySQL；
    // admin-service HttpClient 来源用注入的 factory 替换，不依赖环境变量。
    private static CreateDatabaseHandler Sut(Func<HttpClient>? clientFactory = null) =>
        new(clientFactory ?? (() => throw new InvalidOperationException("测试用例不应该走到调用 admin-service 这一步")));

    [Fact]
    public async Task Execute_InvalidJson_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("not json"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("合法 JSON");
    }

    [Fact]
    public async Task Execute_MissingDatabaseInstanceId_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"databaseName": "ok_db"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("databaseInstanceId");
    }

    [Fact]
    public async Task Execute_MissingDatabaseName_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"databaseInstanceId": 1}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("databaseName");
    }

    [Theory]
    [InlineData("bad-name")]
    [InlineData("x`; DROP DATABASE y; --")]
    public async Task Execute_IllegalDatabaseName_Fails(string databaseName)
    {
        var result = await Sut().ExecuteAsync(
            Context($$"""{"databaseInstanceId": 1, "databaseName": {{System.Text.Json.JsonSerializer.Serialize(databaseName)}}}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("非法");
    }

    [Fact]
    public async Task Execute_IllegalCharset_Fails()
    {
        var result = await Sut().ExecuteAsync(
            Context("""{"databaseInstanceId": 1, "databaseName": "ok_db", "charset": "utf8mb4; DROP"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("charset");
    }

    [Fact]
    public async Task Execute_CredentialsEndpointNotFound_Fails()
    {
        var client = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"databaseInstanceId": 999, "databaseName": "ok_db"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("999");
    }

    [Fact]
    public async Task Execute_AdminServiceBaseUrlNotConfigured_FailsWithEnvVarName()
    {
        var result = await new CreateDatabaseHandler(() => throw new InvalidOperationException($"未配置环境变量 {MySqlPluginHelper.AdminServiceBaseUrlEnvVar}"))
            .ExecuteAsync(Context("""{"databaseInstanceId": 1, "databaseName": "ok_db"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminServiceBaseUrlEnvVar);
    }

    [Fact]
    public void Handler_HasTaskPluginMetadata()
    {
        var metadata = typeof(CreateDatabaseHandler).GetCustomAttribute<TaskPluginAttribute>();

        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("mysql-create-database");
        metadata.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
