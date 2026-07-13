namespace BackendJobService.Api.Auth;

/// <summary>
/// 经网关 ForwardAuth 登录校验后注入的用户身份（deploy/k8s/gateway/auth-middleware.yaml）。
/// 网关会先删除客户端自带的同名头再用 sso-service 校验结果覆盖写入，
/// 因此这两个头仅在请求经网关转发时可信；集群内直连或本地调试时为空。
/// </summary>
public sealed record GatewayUser(ulong UserId, string Username)
{
    public const string UserIdHeader = "X-User-Id";
    public const string UsernameHeader = "X-Username";

    /// <summary>请求未携带网关注入的身份头时返回 null。</summary>
    public static GatewayUser? FromRequest(HttpRequest request)
    {
        var username = request.Headers[UsernameHeader].ToString();
        if (!ulong.TryParse(request.Headers[UserIdHeader].ToString(), out var userId)
            || string.IsNullOrEmpty(username))
        {
            return null;
        }

        return new GatewayUser(userId, username);
    }
}
