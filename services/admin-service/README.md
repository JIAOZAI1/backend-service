# admin-service

管理员服务：面向系统级设置与新用户注册审核开户的管理接口，供拥有 `admin` 角色的用户使用。服务内置 `RequireAdminRoleMiddleware`，拦截除 `/health` 外的所有接口，要求当前用户拥有 `admin` 角色，未登录或角色不含 `admin` 均拒绝访问。

## 目录结构

```bash
admin-service/
├── src/
│   ├── AdminService.Api/                 # 入口层：Controllers（含 ReviewController、TenantsController）、
│   │                                        #   中间件（RequireAdminRoleMiddleware）、
│   │                                        #   网关身份读取（Auth/GatewayUser）、Program.cs、appsettings
│   ├── AdminService.Application/         # 应用层：SystemSettingService/ReviewService/TenantQueryService（用例）、
│   │                                        #   DTO、接口定义、Common（SortSpec、密码/租户 code 生成器）
│   ├── AdminService.Domain/              # 领域层：SystemSetting/Tenant/UserTenant 实体
│   └── AdminService.Infrastructure/      # 基础设施层：EF Core、仓储实现、
│                                            #   ExternalClients（集群内直连 sso-service/backend-job-service）
├── tests/
│   ├── AdminService.UnitTests/
│   └── AdminService.IntegrationTests/
├── configs/
├── migrations/                            # 手写原生 SQL 迁移脚本（系统设置表、租户表/用户租户关系表），见下文
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

## 审核与开户

新用户注册后处于 `pending` 待审核状态（字段维护在 sso-service 的 `users` 表），管理员通过 [`POST /admin-service/api/v1/reviews/{userId}/approve`](#api-说明) 审核通过并触发自动开户，编排逻辑见 [`ReviewService`](src/AdminService.Application/Services/ReviewService.cs)：

1. 调用 sso-service `GET /internal/users/{userID}` 校验用户存在
2. 若该用户尚无租户记录：生成全局唯一的 `tenant_id`（UUID）与 `tenant_code`（12 位小写 base32），写入 `tenants` 表（`status=created`）与 `user_tenants` 关系表；数据库名/用户名固定为 `tenant_{tenant_code}`，密码用 [`SecurePasswordGenerator`](src/AdminService.Application/Common/SecurePasswordGenerator.cs) 生成（16 位，大小写字母+数字+符号，排除易混淆字符）
3. 调用 backend-job-service `POST /backend-job-service/api/v1/jobs` + `POST .../jobs/{jobId}/tasks` 创建一次性作业，依次挂载内置插件 `mysql-create-database`、`mysql-create-user`（见 [backend-job-service README](../backend-job-service/README.md#内置插件-backendjobserviceplugins)）在目标 MySQL 实例上建库建用户并授权
4. 调用 sso-service `PUT /internal/users/{userID}/review` 把用户标记为 `approved`
5. 本地把 `tenants.status` 更新为 `active`（服务期）

**失败处理**：任一步失败均不自动回滚，直接返回 `500 {"failedStep": "...", "message": "..."}`。已落库/已触发的步骤保持原样，管理员可用同一个 `userId` 重新调用同一个审核接口重试——每一步（生成租户/建库建用户/标记已审核）均设计为幂等（`user_tenants` 唯一索引防重复插入，`CreateDatabaseHandler`/`CreateUserHandler` 本身幂等，审核状态更新是覆盖写）。

审核编排调用的 sso-service、backend-job-service 内部接口均走**集群内 Service DNS 直连**（如 `http://sso-service.default.svc.cluster.local`），不经网关，与 sso-service 现有 `/internal/auth/verify` 的设计一致，见 [`AdminService.Infrastructure/ExternalClients`](src/AdminService.Infrastructure/ExternalClients)。

## 本地启动方式

```bash
export ConnectionStrings__MySql="Server=192.168.8.184;Port=3306;Database=sys_db;User=sys_user;Password=xxx;"
export Services__SsoService__BaseUrl="http://sso-service.default.svc.cluster.local"
export Services__JobService__BaseUrl="http://backend-job-service.default.svc.cluster.local"
export TenantDatabase__Host="192.168.8.184"
export TenantDatabase__Port="3306"

make run
```

.NET 的配置系统支持用双下划线 `__` 表示嵌套 key（对应 `appsettings.json` 里的 `ConnectionStrings:MySql`），环境变量优先级高于 `appsettings.{env}.json`，敏感值不写入仓库配置文件，符合规范第 15 章。

依赖 MySQL 8.x。仓库内所有服务共用同一个数据库 `sys_db`（见 f06a0ba「unify infra config via shared config secret and migrate to sys_db」），各服务的表通过表名区分（本服务为 `system_settings`），配置从共享的 `config-dev-secret` 注入（`mysql-host/port/database/username/password`），需自行准备并保证连接地址可达。

本地直连调试（不经网关）时请求不带身份头，会被 `RequireAdminRoleMiddleware` 拒绝；如需本地联调，通过网关转发请求，或手动在请求中附加 `X-User-Id`/`X-Username`/`X-User-Roles`（仅限本地调试，生产环境这些头只信任网关注入的版本）。

## 配置说明

* `appsettings.json`：全局默认值（空的连接串占位）
* `appsettings.{env}.json`：环境名遵循 `dev / test / staging / prod`
* `Services:SsoService:BaseUrl` / `Services:JobService:BaseUrl`：审核编排流程集群内直连的 Service DNS 地址（非敏感信息，直接写实际 Service 名，见 [deploy/k8s/services/admin-service/deployment.yaml](../../deploy/k8s/services/admin-service/deployment.yaml)）
* `TenantDatabase:Host` / `TenantDatabase:Port` / `TenantDatabase:Type`：新租户数据库实际落在的目标 MySQL 实例地址，当前复用与 `sys_db` 相同的实例（`config-dev-secret` 的 `mysql-host`/`mysql-port`）

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
| POST | `/admin-service/api/v1/reviews/{userId}/approve` | 审核用户注册并自动开户，见 [审核与开户](#审核与开户)；用户不存在返回 404，编排失败返回 500 + `{"failedStep", "message"}` |
| GET | `/admin-service/api/v1/tenants` | 分页查询租户列表，支持 `page`/`pageSize`/`sortBy`（`id`/`tenantCode`/`status`/`createdAt`，非法字段 400）/`sortOrder`，响应体固定为 `items`/`page`/`pageSize`/`total` |

`TenantResponse` 不回显 `db_password`（数据库密码只落库，不通过任何查询接口返回）。
