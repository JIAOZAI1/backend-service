using System.Security.Cryptography;
using System.Text;

namespace BackendJobService.Api.Auth;

/// <summary>
/// 校验集群内直连调用（如 admin-service 审核开户流程）携带的共享密钥，用于弥补
/// "不经网关暴露"仅靠网络可达性作为信任边界的不足：即便调用方能连到本服务的 ClusterIP，
/// 没有该密钥也无法调用。仅拦截"创建作业/创建任务"这两个会触发建库建用户等运维操作的写接口，
/// 其余只读接口维持现状不做改动。密钥通过 K8s Secret 下发（见 deploy/k8s/base/secret-dev.yaml
/// 的 internal-api-token），三个服务共享同一份，用固定时间比较防止时序侧信道泄露密钥。
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
