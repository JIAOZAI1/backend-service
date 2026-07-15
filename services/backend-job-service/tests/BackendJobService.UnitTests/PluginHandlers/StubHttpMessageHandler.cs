namespace BackendJobService.UnitTests.PluginHandlers;

/// <summary>测试专用：不发真实网络请求，按注入的 responder 返回预置响应，供插件的 HttpClient 测试用。</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new StubHttpMessageHandler(responder)) { BaseAddress = new Uri("http://stub.local") };
}
