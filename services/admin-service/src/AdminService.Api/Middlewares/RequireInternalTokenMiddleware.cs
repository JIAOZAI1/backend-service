using System.Security.Cryptography;
using System.Text;

namespace AdminService.Api.Middlewares;

/// <summary>
/// 挂在 /internal/* 独立管道分支上（见 Program.cs 的 MapWhen），保护纯供集群内其他服务调用、
/// 不经网关暴露的接口（规范第 16.5 章）。与保护公开业务 API 的场景不同，这里不存在"网关登录
/// 校验通过即放行"的合法路径——/internal/* 从设计上就不应该被前端/网关触达，因此不做
/// GatewayUser 身份头的豁免判断，一律要求 X-Internal-Token 与共享密钥一致，用固定时间比较
/// 防止时序侧信道泄露（同 backend-job-service 的 RequireInternalTokenMiddleware）。
/// </summary>
public class RequireInternalTokenMiddleware(RequestDelegate next, string expectedToken)
{
    public const string InternalTokenHeader = "X-Internal-Token";

    public async Task InvokeAsync(HttpContext context)
    {
        var provided = context.Request.Headers[InternalTokenHeader].ToString();
        if (string.IsNullOrEmpty(provided) || !ConstantTimeEquals(provided, expectedToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "invalid or missing internal token" });
            return;
        }

        await next(context);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
