namespace BackendJobService.Plugins.Internal;

/// <summary>
/// 插件调用其他微服务 /internal/* 接口的公共 HttpClient 构造逻辑。插件由 TaskHandlerLoader 通过
/// Activator.CreateInstance 构造，没有 DI 容器可注入 IHttpClientFactory，因此手动构造 HttpClient，
/// base URL 与共享密钥统一从环境变量读取——密钥复用本服务自身校验入站请求用的同一个环境变量
/// （见 RequireInternalTokenMiddleware 读取的 Internal:Token/Internal__Token），不新增密钥。
/// </summary>
internal static class InternalServiceClientHelper
{
    public const string InternalTokenHeader = "X-Internal-Token";
    public const string InternalTokenEnvVar = "Internal__Token";

    public static HttpClient CreateClient(string baseUrlEnvVar)
    {
        var baseUrl = Environment.GetEnvironmentVariable(baseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException($"未配置环境变量 {baseUrlEnvVar}");
        }

        var token = Environment.GetEnvironmentVariable(InternalTokenEnvVar);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException($"未配置环境变量 {InternalTokenEnvVar}");
        }

        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add(InternalTokenHeader, token);
        return client;
    }
}
