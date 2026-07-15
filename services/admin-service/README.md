# admin-service

管理员服务：面向系统级设置、新用户注册审核开户、数据库实例注册的管理接口，供拥有 `admin` 角色的用户使用。服务内置 `RequireAdminRoleMiddleware`，拦截除 `/health` 外的所有接口，要求当前用户拥有 `admin` 角色，未登录或角色不含 `admin` 均拒绝访问。

## 目录结构

```bash
admin-service/
├── src/
│   ├── AdminService.Api/                 # 入口层：Controllers（含 ReviewController、TenantsController、
│   │                                        #   DatabaseInstancesController、InternalTenantsController、
│   │                                        #   InternalDatabaseInstancesController）、中间件
│   │                                        #   （RequireAdminRoleMiddleware、RequireInternalTokenMiddleware）、
│   │                                        #   网关身份读取（Auth/GatewayUser）、Program.cs、appsettings
│   ├── AdminService.Application/         # 应用层：SystemSettingService/ReviewService/TenantQueryService/
│   │                                        #   DatabaseInstanceService（用例）、DTO、接口定义、
│   │                                        #   Common（SortSpec、密码/租户 code 生成器）
│   ├── AdminService.Domain/              # 领域层：SystemSetting/Tenant/UserTenant/DatabaseInstance 实体
│   └── AdminService.Infrastructure/      # 基础设施层：EF Core、仓储实现、
│                                            #   ExternalClients（集群内直连 sso-service/backend-job-service）、
│                                            #   Security（数据库实例密码加解密，委托给 packages/db-credential-crypto）
├── tests/
│   ├── AdminService.UnitTests/
│   └── AdminService.IntegrationTests/
├── configs/
├── migrations/                            # 手写原生 SQL 迁移脚本（系统设置表、租户表/用户租户关系表、数据库实例表），见下文
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

`/internal/*` 路径不受 `RequireAdminRoleMiddleware` 约束（见 [`Program.cs`](src/AdminService.Api/Program.cs) 的 `MapWhen` 分支），改由 [`RequireInternalTokenMiddleware`](src/AdminService.Api/Middlewares/RequireInternalTokenMiddleware.cs) 校验，见下文"内部接口"。

## 审核与开户

新用户注册后处于 `pending` 待审核状态（字段维护在 sso-service 的 `users` 表），管理员先通过 [`GET /admin-service/api/v1/reviews/users`](#api-说明)（默认 `reviewStatus=pending`）看到待审核用户列表——本接口只是薄转发，内部直连 sso-service 新增的 `GET /internal/users` 分页查询接口，不在 admin-service 侧落库/缓存用户数据。

管理员在开户向导里选定目标数据库实例、填写 License 到期时间后，通过 [`POST /admin-service/api/v1/reviews/{userId}/approve`](#api-说明)（body: `{"databaseInstanceId": ..., "licenseExpiresAt": "..."}`）触发审核开户，编排逻辑见 [`ReviewService`](src/AdminService.Application/Services/ReviewService.cs)。这是一个**异步**流程：同步部分只做校验和创建开户 Job，成功后立即返回 `{"userId", "tenant", "jobId"}`，前端凭 `jobId` 轮询 backend-job-service 的 [`GET /backend-job-service/api/v1/jobs/{jobId}/status`](../backend-job-service/README.md#api-说明) 得知开户是否完成。

管理员也可以拒绝审核：`POST /admin-service/api/v1/reviews/{userId}/reject` 只调用 sso-service 一步，不涉及租户/数据库实例——被拒绝的用户不开户。sso-service 侧会把该用户软删除，**拒绝不可撤销**：拒绝后无法再对同一 `userId` 调用 approve/reject（统一返回 404），但该用户可以用同一 `username`/`email` 重新走注册流程。

同步部分：

1. 校验 `licenseExpiresAt` 晚于当前时间，否则返回 400
2. 调用 sso-service `GET /internal/users/{userID}` 校验用户存在
3. 校验 `databaseInstanceId` 对应的 [`DatabaseInstance`](src/AdminService.Domain/Entities/DatabaseInstance.cs) 存在
4. 若该用户尚无租户记录：生成全局唯一的 `tenant_id`（UUID）与 `tenant_code`（12 位小写 base32），DB 地址取自选中的数据库实例，写入 `tenants` 表（`status=created`，记录 `database_instance_id`、`license_expires_at`）与 `user_tenants` 关系表；数据库名/用户名固定为 `tenant_{tenant_code}`，密码用 [`SecurePasswordGenerator`](src/AdminService.Application/Common/SecurePasswordGenerator.cs) 生成（16 位，大小写字母+数字+符号，排除易混淆字符——这是新建租户数据库用户的密码，与 `DatabaseInstance` 本身的管理员密码是两个不同的密钥）。若该用户已有租户记录（幂等短路），不会用本次传入的 `licenseExpiresAt` 覆盖已设置的到期时间
5. 调用 backend-job-service `POST /backend-job-service/api/v1/jobs` + `POST .../jobs/{jobId}/tasks` 创建一次性开户作业，按顺序挂载四个任务，返回 `jobId`

四个任务按序执行、前一个失败后续不执行（见 [backend-job-service README](../backend-job-service/README.md#内置插件-backendjobserviceplugins)）：

1. `mysql-create-database`：在目标数据库实例上建库
2. `mysql-create-user`：建租户数据库用户并授权
3. `sso-mark-user-reviewed`：调用 sso-service `PUT /internal/users/{userID}/review` 把用户标记为 `approved`
4. `admin-activate-tenant`：调用本服务的 `PUT /internal/tenants/{tenantId}/activate` 把 `tenants.status` 置为 `active`

标记审核完成与激活租户挪到 Job 内的 Task 执行，而不是在 `approve` 接口里同步调用——这样"建用户失败"就不会先于失败被误标记为"已审核"（Task 严格按顺序执行，前一个失败后续不执行）。

**失败处理**：同步部分任一步失败直接返回 `ReviewStepFailedException`（controller 转 500 + `{"failedStep": "...", "message": "..."}`）或 404（用户/数据库实例不存在），不自动回滚；已落库/已触发的步骤保持原样，管理员可用同一个 `userId` 重新调用同一个审核接口重试——每一步（生成租户、四个 Task）均设计为幂等（`user_tenants` 唯一索引防重复插入，`CreateDatabaseHandler`/`CreateUserHandler`/`sso-mark-user-reviewed`/`admin-activate-tenant` 本身幂等）。

审核编排调用的 sso-service、backend-job-service 接口均走**集群内 Service DNS 直连**（如 `http://sso-service.default.svc.cluster.local`），不经网关，与 sso-service 现有 `/internal/auth/verify` 的设计一致，见 [`AdminService.Infrastructure/ExternalClients`](src/AdminService.Infrastructure/ExternalClients)。[`InternalTokenDelegatingHandler`](src/AdminService.Infrastructure/ExternalClients/InternalTokenDelegatingHandler.cs) 会给这些出站请求自动附加 `X-Internal-Token` 请求头（值来自 `Internal:Token` 配置），对方服务侧对应校验这个密钥。

## 内部接口

`/internal/*` 路由不带 `admin-service` 网关前缀、不经网关暴露，供集群内其他服务直连调用（规范第 16.5 章），由 [`RequireInternalTokenMiddleware`](src/AdminService.Api/Middlewares/RequireInternalTokenMiddleware.cs) 校验请求携带的 `X-Internal-Token` 与 `Internal:Token` 配置一致（固定时间比较），不做网关身份头豁免——这些路由从设计上就不应被前端/网关触达。

| Method | Path | 说明 |
| --- | --- | --- |
| GET | `/internal/database-instances/{id}/credentials` | 供 backend-job-service 的建库/建用户插件按 `databaseInstanceId` 现取解密后的连接信息（`dbType`/`host`/`port`/`username`/`password`），不存在返回 404 |
| PUT | `/internal/tenants/{tenantId}/activate` | 供 backend-job-service 的 `admin-activate-tenant` 插件在开户 Job 前置任务全部成功后回写租户状态；`{tenantId}` 是 `Tenant.TenantId`（GUID 业务键），不是自增主键；幂等，已是 `Active` 直接返回；不存在返回 404 |
| PUT | `/internal/tenants/expire-overdue` | 供 backend-job-service 每日 License 监控 Job 的 `admin-expire-overdue-tenants` 插件调用：批量检查所有 `Status=Active` 且 `license_expires_at` 已早于当前时间的租户，置为 `Expired`，返回 `{"expiredCount": N}`；不针对单个租户，没有过期租户时返回 `expiredCount: 0`，不是 404；幂等（已是 `Expired` 的租户天然不在查询范围内）；见 [License 管理](#license-管理) |

三个服务（sso-service/admin-service/backend-job-service）共用同一份 `Internal:Token`（`config-dev-secret` 的 `internal-api-token`）。

## 数据库实例管理

管理员注册系统级数据库实例（[`DatabaseInstance`](src/AdminService.Domain/Entities/DatabaseInstance.cs)），供开户向导选择目标实例、backend-job-service 按 `databaseInstanceId` 执行建库建用户作业：

* 字段：实例名称（`name`，唯一）、数据库类型（`dbType`，目前仅支持 `mysql`，非法值 400）、实例地址（`host`）、端口（`port`，1-65535）、用户名（`username`）、密码
* 密码**只加密后落库**（`encrypted_password` 列），面向管理员的查询接口（列表、详情）均不回显密码——`DatabaseInstanceResponse` 类型上本就没有密码字段，不是运行时过滤；解密后的密码只通过 `/internal/database-instances/{id}/credentials` 这一个内部接口现取现用，供 backend-job-service 的插件执行时调用，不落库、不缓存
* 加密算法固定 AES-256-GCM，实现见共享 SDK [`packages/db-credential-crypto`](../../packages/db-credential-crypto)（Go + .NET 两套实现，密文格式二进制兼容），密钥通过 `DbInstanceEncryptionKey` 配置注入（K8s Secret `config-dev-secret` 的 `db-instance-encryption-key`，见 [`AesGcmDbCredentialCipher`](src/AdminService.Infrastructure/Security/AesGcmDbCredentialCipher.cs)）
* 编辑（`PUT`）时密码字段可选：不传则保留原密文，不会用空值覆盖；传了才重新加密
* 删除为软删除（`deleted_at`），与仓库其他资源一致

## License 管理

租户 License 目前只有一个到期时间字段（`tenants.license_expires_at`），与 `Tenant` 是 1:1 关系，没有单独建表——没有多次续期/历史记录的需求，暂不需要更复杂的模型：

* 到期时间在开户向导里由管理员**手动填写**，随 `POST /admin-service/api/v1/reviews/{userId}/approve` 请求体的 `licenseExpiresAt` 一起提交，必须晚于当前时间（否则 400）
* 已有租户记录的用户重复调用 approve（幂等短路分支）不会覆盖已设置的到期时间
* 每天由 backend-job-service 的一个全局 Cron Job（`admin-expire-overdue-tenants` 插件，见 [backend-job-service README](../backend-job-service/README.md#内置插件-backendjobserviceplugins)）调用本服务的 `PUT /internal/tenants/expire-overdue`，批量把所有 `Status=Active` 且已过期的租户置为 `Expired`——`TenantStatus.Expired` 这个状态值在本次改动前就存在于枚举里但从未被代码设置过，这是它第一次被真正使用
* 监控到期后**只做状态流转**，不做权限收回（比如撤销数据库用户权限）、不做通知——仓库里没有 notification-service，通知渠道超出当前范围，如果以后要加，通知可以作为这个 Cron Job 里追加的下一个 Task，不需要改动现有逻辑
* `Created`/`Cancelled` 状态的租户不参与这个流转：未激活的租户没有"过期"的意义，已取消的也不该被改回 `Expired`

## 用户管理

系统管理员的用户列表（`GET /admin-service/api/v1/users`）与密码重置（`POST /admin-service/api/v1/users/{userId}/reset-password`），与 [审核与开户](#审核与开户) 的待审核列表不同——不限 `reviewStatus`，用于日常全量用户管理，且直接把用户所属租户信息（`tenantCode`/`licenseExpiresAt`）一并返回。

`users`/`roles`/`user_roles` 三张表由 sso-service 拥有（迁移脚本以 [sso-service/migrations](../sso-service/migrations) 为准），本服务与 sso-service 共用同一个 MySQL 数据库 `sys_db`（见上文"本地启动方式"），因此这里选择直接映射同一批表跨库查询/写入，不经 HTTP 调用 sso-service——读（用户列表）和写（密码重置）都是如此：

* [`SsoUser`](src/AdminService.Domain/Entities/SsoUser.cs)/[`SsoRole`](src/AdminService.Domain/Entities/SsoRole.cs)/`SsoUserRole` 是这三张表在本服务里的只读镜像，字段只声明本服务用得到的列，不是 sso-service 对应模型的完整镜像；命名带 `Sso` 前缀，避免与本服务的 `GatewayUser`（网关注入的当前登录管理员身份）混淆
* 用户列表一次查询 `users`，再批量查 `user_roles`/`roles`（角色）与 `user_tenants`/`tenants`（当前 active 租户）后在内存里按 `userId` 聚合——EF Core 对 MySQL 没有可移植的 `GROUP_CONCAT` 映射，拆成多次查询比拼原生 SQL 更符合本服务其他 Repository 的实现风格
* 密码重置：生成随机临时密码（复用 [`SecurePasswordGenerator`](src/AdminService.Application/Common/SecurePasswordGenerator.cs)，16 位，与开户流程生成租户数据库密码用的是同一个生成器），用 `BCrypt.Net-Next` 以 work factor 10 计算哈希后直接 `UPDATE users SET password_hash=...`——与 sso-service 侧 `bcrypt.DefaultCost`（Go `golang.org/x/crypto/bcrypt` 定义为 10）保持一致；bcrypt 密文自带 cost 参数，写入方是哪个语言实现不影响登录时的校验。新密码只在这一次响应里明文返回，不落库、不记录日志，管理员需当场转告用户
* 与 sso-service 的软删除语义保持一致：`SsoUserConfiguration` 对 `deleted_at` 加了 `HasQueryFilter`，被拒绝审核（软删除，见 sso-service README"用户审核字段说明"）的用户不会出现在列表里，也无法被重置密码（`GetUserByIdAsync` 查不到）

## 本地启动方式

```bash
export ConnectionStrings__MySql="Server=192.168.8.184;Port=3306;Database=sys_db;User=sys_user;Password=xxx;"
export Services__SsoService__BaseUrl="http://sso-service.default.svc.cluster.local"
export Services__JobService__BaseUrl="http://backend-job-service.default.svc.cluster.local"
export Internal__Token="xxx"
export DbInstanceEncryptionKey="xxx"   # base64 编码的 32 字节 AES-256 密钥，用 openssl rand -base64 32 生成

make run
```

.NET 的配置系统支持用双下划线 `__` 表示嵌套 key（对应 `appsettings.json` 里的 `ConnectionStrings:MySql`），环境变量优先级高于 `appsettings.{env}.json`，敏感值不写入仓库配置文件，符合规范第 15 章。

依赖 MySQL 8.x。仓库内所有服务共用同一个数据库 `sys_db`（见 f06a0ba「unify infra config via shared config secret and migrate to sys_db」），各服务的表通过表名区分（本服务为 `system_settings`），配置从共享的 `config-dev-secret` 注入（`mysql-host/port/database/username/password`），需自行准备并保证连接地址可达。

本地直连调试（不经网关）时请求不带身份头，会被 `RequireAdminRoleMiddleware` 拒绝；如需本地联调，通过网关转发请求，或手动在请求中附加 `X-User-Id`/`X-Username`/`X-User-Roles`（仅限本地调试，生产环境这些头只信任网关注入的版本）。

## 配置说明

* `appsettings.json`：全局默认值（空的连接串占位）
* `appsettings.{env}.json`：环境名遵循 `dev / test / staging / prod`
* `Services:SsoService:BaseUrl` / `Services:JobService:BaseUrl`：审核编排流程集群内直连的 Service DNS 地址（非敏感信息，直接写实际 Service 名，见 [deploy/k8s/services/admin-service/deployment.yaml](../../deploy/k8s/services/admin-service/deployment.yaml)）
* `Internal:Token`：既用于调用 sso-service/backend-job-service 内部接口时自动附加的共享密钥，也用于校验 backend-job-service 反查本服务 `/internal/*` 接口时携带的同一个密钥（见 [内部接口](#内部接口)），未配置时启动直接抛异常
* `DbInstanceEncryptionKey`：数据库实例密码的 AES-256-GCM 加密密钥（base64 编码 32 字节），`config-dev-secret` 的 `db-instance-encryption-key`，未配置或长度不对时启动直接抛异常；轮换密钥前需要先用旧密钥解密全部现有数据再用新密钥重新加密，否则旧密文无法解密

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
| GET | `/admin-service/api/v1/reviews/users` | 分页查询指定审核状态的待审核用户列表，支持 `reviewStatus`（默认 `pending`，也可传 `approved`/`rejected`）/`page`/`pageSize`/`sortBy`（`id`/`createdAt`，非法字段 400）/`sortOrder`，响应体固定为 `items`/`page`/`pageSize`/`total`；内部转发 sso-service 查询，见 [审核与开户](#审核与开户) |
| POST | `/admin-service/api/v1/reviews/{userId}/approve` | 审核用户注册并触发开户 Job（body: `{"databaseInstanceId", "licenseExpiresAt"}`），异步：立即返回 `{"userId", "tenant", "jobId"}`，见 [审核与开户](#审核与开户)；`licenseExpiresAt` 早于当前时间返回 400，用户/数据库实例不存在返回 404，编排失败返回 500 + `{"failedStep", "message"}` |
| POST | `/admin-service/api/v1/reviews/{userId}/reject` | 拒绝用户注册（不开户），sso-service 侧软删除该用户，拒绝不可撤销，见 [审核与开户](#审核与开户)；用户不存在返回 404，调用 sso-service 失败返回 500 + `{"failedStep", "message"}` |
| GET | `/admin-service/api/v1/tenants` | 分页查询租户列表，支持 `page`/`pageSize`/`sortBy`（`id`/`tenantCode`/`status`/`createdAt`，非法字段 400）/`sortOrder`，响应体固定为 `items`/`page`/`pageSize`/`total` |
| GET | `/admin-service/api/v1/database-instances` | 分页查询数据库实例列表，支持 `page`/`pageSize`/`sortBy`（`id`/`name`/`dbType`/`createdAt`/`updatedAt`，非法字段 400）/`sortOrder`，响应体固定为 `items`/`page`/`pageSize`/`total` |
| GET | `/admin-service/api/v1/database-instances/{id}` | 查询指定数据库实例，不存在返回 404 |
| POST | `/admin-service/api/v1/database-instances` | 注册数据库实例（body: `{"name", "dbType", "host", "port", "username", "password"}`），`dbType` 目前仅支持 `mysql`，`name` 重复或字段非法返回 400 |
| PUT | `/admin-service/api/v1/database-instances/{id}` | 编辑数据库实例（body: `{"name", "host", "port", "username", "password"}`，`password` 可省略以保留原密码），不存在返回 404，`name` 与其他实例重复或字段非法返回 400 |
| DELETE | `/admin-service/api/v1/database-instances/{id}` | 软删除数据库实例，不存在返回 404 |
| GET | `/admin-service/api/v1/users` | 分页查询全量用户（不限 reviewStatus），含角色与当前 active 租户信息（`tenantCode`/`licenseExpiresAt`，未开户或租户非 active 时为空），支持 `page`/`pageSize`/`sortBy`（`id`/`username`/`createdAt`，非法字段 400）/`sortOrder`，响应体固定为 `items`/`page`/`pageSize`/`total`，见 [用户管理](#用户管理) |
| POST | `/admin-service/api/v1/users/{userId}/reset-password` | 重置用户密码为随机临时密码，响应 `{"newPassword"}` 明文只返回一次，见 [用户管理](#用户管理)；用户不存在返回 404 |

`TenantResponse`/`DatabaseInstanceResponse` 均不回显密码字段（数据库密码只落库，不通过任何查询接口返回）；`DatabaseInstance` 密码额外加密后落库，见[数据库实例管理](#数据库实例管理)。
