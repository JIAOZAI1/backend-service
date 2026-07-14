using AdminService.Api.Auth;

namespace AdminService.Api.Middlewares;

/// <summary>
/// 拦截本服务全部接口（/health 除外），要求当前用户拥有 admin 角色。
/// 用户身份与角色来自网关 ForwardAuth 注入的 X-User-Id/X-Username/X-User-Roles 请求头
/// （见 GatewayUser、deploy/k8s/gateway/auth-middleware.yaml），角色由 sso-service
/// 每次请求实时查库返回、不依赖 JWT 快照，因此角色变更（如撤销 admin）对后续请求立即生效。
/// 缺少身份头（未经网关转发）或角色中不含 admin 均拒绝访问。
/// </summary>
public class RequireAdminRoleMiddleware(RequestDelegate next)
{
    public const string AdminRole = "admin";

    public async Task InvokeAsync(HttpContext context)
    {
        // /health 供 K8s 探针直接访问 Pod，不经过网关、不带身份头，必须放行
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var user = GatewayUser.FromRequest(context.Request);
        if (user is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "missing gateway identity headers" });
            return;
        }

        if (!user.HasRole(AdminRole))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "admin role required" });
            return;
        }

        context.Items[nameof(GatewayUser)] = user;
        await next(context);
    }
}
