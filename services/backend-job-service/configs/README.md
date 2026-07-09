# configs

.NET 服务的运行时配置遵循 ASP.NET Core 惯例，实际生效的配置文件位于 `src/BackendJobService.Api/appsettings.json` 与 `appsettings.{env}.json`（不是本目录），环境变量可覆盖同名 key（如 `ConnectionStrings__MySql`）。

本目录保留用于放置非 ASP.NET Core 配置系统直接加载的部署期配置模板（如有）。
