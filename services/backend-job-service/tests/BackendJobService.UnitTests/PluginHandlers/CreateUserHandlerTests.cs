using System.Net;
using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class CreateUserHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    // 校验失败必须发生在调用 admin-service/连接数据库之前，因此这些用例不需要真实的 HttpClient/MySQL；
    // admin-service HttpClient 来源用注入的 factory 替换，不依赖环境变量。
    private static CreateUserHandler Sut(Func<HttpClient>? clientFactory = null) =>
        new(clientFactory ?? (() => throw new InvalidOperationException("测试用例不应该走到调用 admin-service 这一步")));

    [Fact]
    public async Task Execute_MissingDatabaseInstanceId_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"username": "svc_user", "password": "x"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("databaseInstanceId");
    }

    [Fact]
    public async Task Execute_MissingUsername_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"databaseInstanceId": 1, "password": "x"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("username");
    }

    [Fact]
    public async Task Execute_MissingPassword_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"databaseInstanceId": 1, "username": "svc_user"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("password");
    }

    [Theory]
    [InlineData("bad-user")]
    [InlineData("u'; DROP USER x; --")]
    [InlineData("this_username_is_way_longer_than_32_chars")]
    public async Task Execute_IllegalUsername_Fails(string username)
    {
        var result = await Sut().ExecuteAsync(
            Context($$"""{"databaseInstanceId": 1, "username": {{System.Text.Json.JsonSerializer.Serialize(username)}}, "password": "x"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("username");
    }

    [Fact]
    public async Task Execute_IllegalHost_Fails()
    {
        var result = await Sut().ExecuteAsync(
            Context("""{"databaseInstanceId": 1, "username": "svc_user", "password": "x", "host": "h'ost"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("host");
    }

    [Fact]
    public async Task Execute_IllegalGrantDatabase_Fails()
    {
        var result = await Sut().ExecuteAsync(
            Context("""{"databaseInstanceId": 1, "username": "svc_user", "password": "x", "grantDatabase": "bad-db"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("grantDatabase");
    }

    [Fact]
    public async Task Execute_PrivilegeNotInWhitelist_Fails()
    {
        var result = await Sut().ExecuteAsync(
            Context("""{"databaseInstanceId": 1, "username": "svc_user", "password": "x", "grantDatabase": "ok_db", "privileges": ["SUPER"]}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("SUPER");
    }

    [Fact]
    public async Task Execute_CredentialsEndpointNotFound_Fails()
    {
        var client = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"databaseInstanceId": 999, "username": "svc_user", "password": "x"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("999");
    }

    [Fact]
    public async Task Execute_AdminServiceBaseUrlNotConfigured_FailsWithEnvVarName()
    {
        var result = await new CreateUserHandler(() => throw new InvalidOperationException($"未配置环境变量 {MySqlPluginHelper.AdminServiceBaseUrlEnvVar}"))
            .ExecuteAsync(Context("""{"databaseInstanceId": 1, "username": "svc_user", "password": "x"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminServiceBaseUrlEnvVar);
    }

    [Fact]
    public void Handler_HasTaskPluginMetadata()
    {
        var metadata = typeof(CreateUserHandler).GetCustomAttribute<TaskPluginAttribute>();

        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("mysql-create-user");
        metadata.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
