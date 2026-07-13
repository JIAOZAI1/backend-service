using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class CreateUserHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    // 校验失败必须发生在连接数据库之前，因此这些用例不需要 MySQL 实例；
    // 连接串来源用注入的 provider 替换，不依赖环境变量。
    private readonly CreateUserHandler _sut = new(adminDsnProvider: () => null);

    [Fact]
    public async Task Execute_MissingUsername_Fails()
    {
        var result = await _sut.ExecuteAsync(Context("""{"password": "x"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("username");
    }

    [Fact]
    public async Task Execute_MissingPassword_Fails()
    {
        var result = await _sut.ExecuteAsync(Context("""{"username": "svc_user"}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("password");
    }

    [Theory]
    [InlineData("bad-user")]
    [InlineData("u'; DROP USER x; --")]
    [InlineData("this_username_is_way_longer_than_32_chars")]
    public async Task Execute_IllegalUsername_Fails(string username)
    {
        var result = await _sut.ExecuteAsync(
            Context($$"""{"username": {{System.Text.Json.JsonSerializer.Serialize(username)}}, "password": "x"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("username");
    }

    [Fact]
    public async Task Execute_IllegalHost_Fails()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"username": "svc_user", "password": "x", "host": "h'ost"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("host");
    }

    [Fact]
    public async Task Execute_IllegalGrantDatabase_Fails()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"username": "svc_user", "password": "x", "grantDatabase": "bad-db"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("grantDatabase");
    }

    [Fact]
    public async Task Execute_PrivilegeNotInWhitelist_Fails()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"username": "svc_user", "password": "x", "grantDatabase": "ok_db", "privileges": ["SUPER"]}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("SUPER");
    }

    [Fact]
    public async Task Execute_AdminDsnNotConfigured_FailsWithEnvVarName()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"username": "svc_user", "password": "x"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminDsnEnvVar);
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
