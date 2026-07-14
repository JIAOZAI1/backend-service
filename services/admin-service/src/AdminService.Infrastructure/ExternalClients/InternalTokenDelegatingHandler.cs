namespace AdminService.Infrastructure.ExternalClients;

/// <summary>
/// 给集群内直连调用（sso-service/backend-job-service 的内部接口）自动附加共享密钥请求头，
/// 弥补"不经网关暴露"仅靠网络可达性作为信任边界的不足。密钥通过 K8s Secret 下发
/// （见 deploy/k8s/base/secret-dev.yaml 的 internal-api-token），三个服务共享同一份。
/// </summary>
public class InternalTokenDelegatingHandler(string internalToken) : DelegatingHandler
{
    public const string InternalTokenHeader = "X-Internal-Token";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(InternalTokenHeader, internalToken);
        return base.SendAsync(request, cancellationToken);
    }
}
