using System.Net;
using System.Reflection;
using BackendJobService.Contracts;
using BackendJobService.Plugins.Sso;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class MarkUserReviewedHandlerTests
{
    private static TaskExecutionContext Context(string parametersJson) => new() { ParametersJson = parametersJson };

    private static MarkUserReviewedHandler Sut(Func<HttpClient>? clientFactory = null) =>
        new(clientFactory ?? (() => throw new InvalidOperationException("测试用例不应该走到调用 sso-service 这一步")));

    [Fact]
    public async Task Execute_InvalidJson_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("not json"), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("合法 JSON");
    }

    [Fact]
    public async Task Execute_MissingUserId_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"reviewedBy": 1}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("userId");
    }

    [Fact]
    public async Task Execute_MissingReviewedBy_Fails()
    {
        var result = await Sut().ExecuteAsync(Context("""{"userId": 42}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("reviewedBy");
    }

    [Fact]
    public async Task Execute_UserNotFound_Fails()
    {
        var client = StubHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"userId": 42, "reviewedBy": 1}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain("42");
    }

    [Fact]
    public async Task Execute_Success_ReturnsOk()
    {
        var client = StubHttpMessageHandler.CreateClient(request =>
        {
            request.Method.ShouldBe(HttpMethod.Put);
            request.RequestUri!.AbsolutePath.ShouldBe("/internal/users/42/review");
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await Sut(() => client).ExecuteAsync(
            Context("""{"userId": 42, "reviewedBy": 1}"""), CancellationToken.None);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_BaseUrlNotConfigured_FailsWithEnvVarName()
    {
        var result = await new MarkUserReviewedHandler(() => throw new InvalidOperationException($"未配置环境变量 {MarkUserReviewedHandler.SsoServiceBaseUrlEnvVar}"))
            .ExecuteAsync(Context("""{"userId": 42, "reviewedBy": 1}"""), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull().ShouldContain(MarkUserReviewedHandler.SsoServiceBaseUrlEnvVar);
    }

    [Fact]
    public void Handler_HasTaskPluginMetadata()
    {
        var metadata = typeof(MarkUserReviewedHandler).GetCustomAttribute<TaskPluginAttribute>();

        metadata.ShouldNotBeNull();
        metadata.Name.ShouldBe("sso-mark-user-reviewed");
        metadata.Description.ShouldNotBeNullOrWhiteSpace();
    }
}
