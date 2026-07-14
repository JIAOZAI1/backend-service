using BackendJobService.Api.Auth;
using BackendJobService.Application;
using BackendJobService.Infrastructure;

namespace BackendJobService.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);

        var internalToken = builder.Configuration["Internal:Token"];
        if (string.IsNullOrEmpty(internalToken))
        {
            throw new InvalidOperationException("Internal:Token (INTERNAL_API_TOKEN) is not configured");
        }

        var app = builder.Build();

        // 仓库环境命名统一用 dev/test/staging/prod（见规范第 14 章），不使用
        // ASP.NET Core 内置的 IsDevelopment()/IsStaging()/IsProduction()——那组方法
        // 只识别固定字符串 "Development"/"Staging"/"Production"，与仓库命名不一致。
        if (string.Equals(app.Environment.EnvironmentName, "dev", StringComparison.OrdinalIgnoreCase))
        {
            app.MapOpenApi();
        }

        // 健康检查不带网关路由前缀：K8s 探针直接访问 Pod，不经过网关
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // 仅拦截创建作业/创建任务这两个写接口，其余只读接口维持现状，见 RequireInternalTokenMiddleware
        app.UseMiddleware<RequireInternalTokenMiddleware>(internalToken);

        app.MapControllers();

        app.Run();
    }
}
