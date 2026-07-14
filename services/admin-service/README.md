# admin-service

管理员服务：面向系统级设置的管理接口，供拥有 `admin` 角色的用户使用。服务内置 `RequireAdminRoleMiddleware`，拦截除 `/health` 外的所有接口，要求当前用户拥有 `admin` 角色，未登录或角色不含 `admin` 均拒绝访问。

## 目录结构

```bash
admin-service/
├── src/
│   ├── AdminService.Api/                 # 入口层：Controllers、中间件（RequireAdminRoleMiddleware）、
│   │                                        #   网关身份读取（Auth/GatewayUser）、Program.cs、appsettings
│   ├── AdminService.Application/         # 应用层：SystemSettingService（用例）、DTO、接口定义
│   ├── AdminService.Domain/              # 领域层：SystemSetting 实体
│   └── AdminService.Infrastructure/      # 基础设施层：EF Core、仓储实现
├── tests/
│   ├── AdminService.UnitTests/
│   └── AdminService.IntegrationTests/
├── configs/
├── migrations/                            # 手写原生 SQL 迁移脚本，见下文
├── AdminService.slnx
├── Dockerfile
├── Makefile
├── service.yaml
└── README.md
```

## 权限模型

用户身份与角色**不**在本服务内解析 JWT，而是信任网关 ForwardAuth 校验后注入的请求头（见 [deploy/k8s/gateway/auth-middleware.yaml](../../deploy/k8s/gateway/auth-middleware.yaml)、sso-service 的 `/internal/auth/verify`）：

| Header | 说明 |
| --- | --- |
| `X-User-Id` | 当前用户 ID |
| `X-Username` | 当前用户名 |
| `X-User-Roles` | 逗号分隔的角色名列表，sso-service 每次请求实时查库生成，不依赖 JWT 快照，角色变更（如撤销 admin）对下一次请求立即生效 |

这些头只有**经网关转发**的请求才可信；集群内直连或本地调试时为空，`GatewayUser.FromRequest` 会返回 `null`，`RequireAdminRoleMiddleware` 按未登录处理（401）。

`RequireAdminRoleMiddleware`（[src/AdminService.Api/Middlewares/RequireAdminRoleMiddleware.cs](src/AdminService.Api/Middlewares/RequireAdminRoleMiddleware.cs)）拦截 `/health` 之外的所有请求：

* 缺少网关身份头 → 401
* 角色列表不含 `admin` → 403
* 校验通过 → 将 `GatewayUser` 存入 `HttpContext.Items` 供后续 Controller 使用，放行

角色管理（分配/撤销 `admin` 角色）由 sso-service 负责，见 [sso-service README](../sso-service/README.md#api-说明) 的角色管理接口。

## 本地启动方式

```bash
export ConnectionStrings__MySql="Server=192.168.8.184;Port=3306;Database=sys_db;User=sys_user;Password=xxx;"

make run
```

.NET 的配置系统支持用双下划线 `__` 表示嵌套 key（对应 `appsettings.json` 里的 `ConnectionStrings:MySql`），环境变量优先级高于 `appsettings.{env}.json`，敏感值不写入仓库配置文件，符合规范第 15 章。

依赖 MySQL 8.x。仓库内所有服务共用同一个数据库 `sys_db`（见 f06a0ba「unify infra config via shared config secret and migrate to sys_db」），各服务的表通过表名区分（本服务为 `system_settings`），配置从共享的 `config-dev-secret` 注入（`mysql-host/port/database/username/password`），需自行准备并保证连接地址可达。

本地直连调试（不经网关）时请求不带身份头，会被 `RequireAdminRoleMiddleware` 拒绝；如需本地联调，通过网关转发请求，或手动在请求中附加 `X-User-Id`/`X-Username`/`X-User-Roles`（仅限本地调试，生产环境这些头只信任网关注入的版本）。

## 配置说明

* `appsettings.json`：全局默认值（空的连接串占位）
* `appsettings.{env}.json`：环境名遵循 `dev / test / staging / prod`

## 数据库迁移

迁移脚本是手写的原生 SQL（`migrations/*.up.sql` / `*.down.sql`），不使用 EF Core 自带的迁移机制，与仓库其他服务的迁移方式保持一致（[golang-migrate](https://github.com/golang-migrate/migrate)）。

```bash
export MYSQL_PASSWORD=xxx
make migrate-up      # 应用所有未执行的迁移
make migrate-down     # 回滚最近一次迁移
```

新增表结构变更时，在 `migrations/` 下按 `NNNNNN_description.up.sql` / `.down.sql` 命名新增一对文件（序号递增），同时手动同步更新 `Infrastructure/Persistence/Configurations/` 下对应的 EF Core 实体配置。

## API 说明

服务路由前缀：`/admin-service`（详见根目录 [微服务项目开发规范.md](../../微服务项目开发规范.md) 第 16.3 节，网关按 host + 前缀转发且不 strip）。全部接口要求 `admin` 角色。

Base path: `/admin-service/api/v1`

| Method | Path | 说明 |
| --- | --- | --- |
| GET | `/admin-service/api/v1/settings` | 列出所有系统级设置 |
| GET | `/admin-service/api/v1/settings/{key}` | 查询指定设置，不存在返回 404 |
| PUT | `/admin-service/api/v1/settings/{key}` | 创建或更新指定设置（body: `{"value": "...", "description": "..."}`） |
