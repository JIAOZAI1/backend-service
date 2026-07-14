namespace AdminService.Infrastructure.ExternalClients;

/// <summary>集群内直连其他服务的 Service DNS 地址（不经网关），从配置 Services:SsoService / Services:JobService 注入。</summary>
public class ExternalServiceOptions
{
    public required string BaseUrl { get; init; }
}
