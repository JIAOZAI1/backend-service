using AdminService.Api.Middlewares;
using AdminService.Application;
using AdminService.Infrastructure;

namespace AdminService.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);

        var internalToken = builder.Configuration["Internal:Token"]
            ?? throw new InvalidOperationException("Internal:Token is not configured");

        var app = builder.Build();

        // 仓库环境命名统一用 dev/test/staging/prod（见规范第 14 章），不使用
        // ASP.NET Core 内置的 IsDevelopment()/IsStaging()/IsProduction()——那组方法
        // 只识别固定字符串 "Development"/"Staging"/"Production"，与仓库命名不一致。
        if (string.Equals(app.Environment.EnvironmentName, "dev", StringComparison.OrdinalIgnoreCase))
        {
            app.MapOpenApi();
        }

        // 健康检查不带网关路由前缀、不经过管理员校验：K8s 探针直接访问 Pod，不经过网关
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // /internal/* 分流到独立管道：不经网关暴露，不要求管理员登录身份，改由
        // RequireInternalTokenMiddleware 校验集群内共享密钥（规范第 16.5 章）。分支内需要
        // 显式重新声明路由与终结点映射——顶层 app.MapControllers() 只挂在主管道，不会
        // 延伸到 MapWhen 的子分支。
        app.MapWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/internal"),
            internalApp =>
            {
                internalApp.UseMiddleware<RequireInternalTokenMiddleware>(internalToken);
                internalApp.UseRouting();
                internalApp.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        // 本服务的所有其他接口都要求 admin 角色，见 RequireAdminRoleMiddleware
        app.UseMiddleware<RequireAdminRoleMiddleware>();

        app.MapControllers();

        app.Run();
    }
}
