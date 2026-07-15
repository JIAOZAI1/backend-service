using System.Security.Cryptography;
using System.Text;

namespace BackendJobService.Api.Auth;

/// <summary>
/// 创建作业/创建任务是本服务对外的公开业务 API（规范 16.5.3：即使当前只被 admin-service 等
/// 集群内服务调用，也不算"仅内部调用接口"，不能要求所有调用方都携带内部共享密钥），
/// 前端会经网关正常调用这两个接口。这里放行两类调用方：
/// 1) 经网关 ForwardAuth 登录校验后转发的请求——带有网关注入且客户端无法伪造的
///    X-User-Id/X-Username 头（见 GatewayUser、deploy/k8s/gateway/auth-middleware.yaml）；
/// 2) 集群内直连调用（如 admin-service 审核开户流程）——携带与本服务一致的共享密钥
///    X-Internal-Token，用于弥补"不经网关暴露"仅靠网络可达性作为信任边界的不足。
/// 两者都没有的请求（如未登录的匿名直连）才拒绝。密钥通过 K8s Secret 下发
/// （见 deploy/k8s/base/secret-dev.yaml 的 internal-api-token），用固定时间比较防止时序侧信道泄露。
/// </summary>
public class RequireInternalTokenMiddleware(RequestDelegate next, string expectedToken)
{
    public const string InternalTokenHeader = "X-Internal-Token";

    private static readonly (string Method, PathString Path)[] ProtectedExactRoutes =
    [
        (HttpMethods.Post, new PathString("/backend-job-service/api/v1/jobs")),
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsProtectedRoute(context.Request))
        {
            await next(context);
            return;
        }

        if (GatewayUser.FromRequest(context.Request) is not null)
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[InternalTokenHeader].ToString();
        if (string.IsNullOrEmpty(provided) || !ConstantTimeEquals(provided, expectedToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "invalid or missing internal token" });
            return;
        }

        await next(context);
    }

    private static bool IsProtectedRoute(HttpRequest request)
    {
        // POST /jobs（新建作业）
        foreach (var (method, path) in ProtectedExactRoutes)
        {
            if (HttpMethods.Equals(request.Method, method) && request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // POST /jobs/{jobId}/tasks（新建任务）
        if (HttpMethods.Equals(request.Method, HttpMethods.Post)
            && request.Path.StartsWithSegments("/backend-job-service/api/v1/jobs", out var remaining)
            && remaining.Value?.EndsWith("/tasks", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
