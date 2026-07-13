using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class CreateDatabaseHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    // 校验失败必须发生在连接数据库之前，因此这些用例不需要 MySQL 实例；
    // 连接串来源用注入的 provider 替换，不依赖环境变量。
    private readonly CreateDatabaseHandler _sut = new(adminDsnProvider: () => null);

    [Fact]
    public async Task Execute_InvalidJson_Fails()
    {
        var result = await _sut.ExecuteAsync(Context("not json"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("合法 JSON");
    }

    [Fact]
    public async Task Execute_MissingDatabaseName_Fails()
    {
        var result = await _sut.ExecuteAsync(Context("{}"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("databaseName");
    }

    [Theory]
    [InlineData("bad-name")]
    [InlineData("x`; DROP DATABASE y; --")]
    public async Task Execute_IllegalDatabaseName_Fails(string databaseName)
    {
        var result = await _sut.ExecuteAsync(
            Context($$"""{"databaseName": {{System.Text.Json.JsonSerializer.Serialize(databaseName)}}}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("非法");
    }

    [Fact]
    public async Task Execute_IllegalCharset_Fails()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"databaseName": "ok_db", "charset": "utf8mb4; DROP"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("charset");
    }

    [Fact]
    public async Task Execute_AdminDsnNotConfigured_FailsWithEnvVarName()
    {
        var result = await _sut.ExecuteAsync(
            Context("""{"databaseName": "ok_db"}"""),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MySqlPluginHelper.AdminDsnEnvVar);
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
