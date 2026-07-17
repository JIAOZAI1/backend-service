# sso-service

统一 JWT 登录服务，提供注册、登录、注销、Token 续期能力，供其他微服务作为统一身份认证入口。同时提供基于角色（RBAC）的权限管理：新用户注册自动分配 `default` 角色，登录/续期/`/me` 返回角色列表，并提供 `admin` 角色专属的角色管理接口。

## 目录结构

```bash
sso-service/
├── cmd/server/            # 程序入口（main.go）
├── internal/
│   ├── handler/           # HTTP 入口，参数解析与响应封装（auth_handler.go、role_handler.go）
│   ├── service/            # 业务逻辑编排（注册/登录/续期/注销、角色管理）
│   ├── repository/         # 数据访问：MySQL(用户、角色) + Redis(refresh token/黑名单)
│   ├── model/               # 领域模型 / DTO（user.go、role.go、dto.go）
│   ├── config/               # 配置加载（viper）
│   └── middleware/            # JWT 鉴权中间件、角色校验中间件（RequireRole）
├── pkg/jwtutil/            # JWT 签发与解析，可被其他服务安全引用
├── tests/                 # 集成测试
├── configs/                # 服务级配置模板（app.<env>.yaml）
├── migrations/              # 数据库迁移脚本（用户表、角色表、用户审核字段）
├── Dockerfile
├── Makefile
├── service.yaml
└── README.md
```

## 本地启动方式

```bash
export MYSQL_PASSWORD=xxx
export REDIS_PASSWORD=xxx
export JWT_SECRET=xxx
export INTERNAL_API_TOKEN=xxx
export SSO_APP_ENV=dev   # 对应 configs/app.dev.yaml

make run
```

依赖 MySQL 8.x 与 Redis 6.x，需自行准备并保证 `configs/app.dev.yaml` 中的连接地址可达。仓库内所有服务共用同一个数据库 `sys_db`，各服务的表通过表名区分（本服务为 `users`/`roles`/`user_roles`），数据库名从环境变量 `MYSQL_DATABASE` 注入（见 [deploy/k8s/services/sso-service/deployment.yaml](../../deploy/k8s/services/sso-service/deployment.yaml)，与 admin-service/backend-job-service 共用同一份 `config-dev-secret`）。

## 配置说明

配置模板位于 `configs/app.<env>.yaml`，环境仅允许 `dev / test / staging / prod`。

## 环境变量说明

| 变量 | 说明 |
| --- | --- |
| `SSO_APP_ENV` | 运行环境，决定加载哪个 `app.<env>.yaml`，默认 `dev` |
| `MYSQL_PASSWORD` | MySQL 密码，注入到 DSN 中 |
| `REDIS_PASSWORD` | Redis 密码 |
| `JWT_SECRET` | JWT 签名密钥，生产环境必须通过 Secret 管理注入，禁止硬编码 |
| `INTERNAL_API_TOKEN` | 集群内服务间调用共享密钥，`/internal/users/...` 接口用它校验调用方（见下文），与 admin-service/backend-job-service 共用同一份（`config-dev-secret` 的 `internal-api-token`），生产环境必须通过 Secret 管理注入，禁止硬编码。启动时未配置直接 `log.Fatal` |

## API 说明

服务路由前缀：`/sso-service`（详见根目录 [微服务项目开发规范.md](../../微服务项目开发规范.md) 第 16.3 节及 [docs/route-mapping.md](../../docs/route-mapping.md)，网关按 host + 前缀转发且不 strip，因此服务内部路由必须自带此前缀）。

Base path: `/sso-service/api/v1`

#### 认证接口（`/auth`）

| Method | Path | 说明 |
| --- | --- | --- |
| POST | `/sso-service/api/v1/auth/register` | 注册新用户，自动分配 `default` 角色 |
| POST | `/sso-service/api/v1/auth/login` | 登录，返回 access/refresh token 及角色列表 |
| POST | `/sso-service/api/v1/auth/refresh` | 使用 refresh token 换取新的 token 对（旧 refresh token 立即失效），响应含角色列表 |
| POST | `/sso-service/api/v1/auth/logout` | 注销，使 refresh token 失效 |
| GET  | `/sso-service/api/v1/auth/me` | 需 `Authorization: Bearer <access token>`，返回当前用户信息及角色列表 |

#### 角色管理接口（需登录 + `admin` 角色）

| Method | Path | 说明 |
| --- | --- | --- |
| GET    | `/sso-service/api/v1/roles` | 列出所有角色 |
| POST   | `/sso-service/api/v1/roles` | 新建角色 |
| GET    | `/sso-service/api/v1/users/:userID/roles` | 查询指定用户的角色列表 |
| POST   | `/sso-service/api/v1/users/:userID/roles` | 给指定用户分配角色（body: `{"roleName": "..."}`） |
| DELETE | `/sso-service/api/v1/users/:userID/roles/:roleName` | 移除指定用户的某个角色 |

#### 内部接口（不经网关暴露）

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| GET | `/internal/auth/verify` | 供网关 ForwardAuth 调用（见 [deploy/k8s/gateway/auth-middleware.yaml](../../deploy/k8s/gateway/auth-middleware.yaml)）：校验 `Authorization: Bearer <access token>` 并查 Redis 黑名单，通过返回 204 及 `X-User-Id`/`X-Username`/`X-User-Roles`（逗号分隔的角色名，实时查库、不依赖 JWT 快照）/`X-Tenant-Code`（当前用户所属 active 租户的 tenant_code，同样实时查库；找不到 active 租户时该头缺失，不是错误——未开户、租户还在 created/审核中、已 expired/cancelled 均属此类）响应头，失败返回 401。与 `/health` 一样不带网关前缀，仅集群内直连本服务可达 |
| GET | `/internal/users` | 供 admin-service 开户向导展示待审核用户列表：分页查询指定 `reviewStatus` 的用户（query：`reviewStatus` 默认 `pending`，也可传 `approved`/`rejected`；`page`/`pageSize`/`sortBy`（`id`/`createdAt`）/`sortOrder`，遵循规范第 16.4 章），响应体固定为 `items`/`page`/`pageSize`/`total`。`reviewStatus`/`sortBy` 非法值返回 400 |
| GET | `/internal/users/{userID}` | 供 admin-service 审核开户流程集群内直连调用：返回用户基本信息（`id`/`username`/`email`/`status`/`reviewStatus`），不存在返回 404（含已被拒绝审核的用户，见下） |
| PUT | `/internal/users/{userID}/review` | 供 admin-service 审核通过后调用（body: `{"reviewedBy": <管理员用户ID>}`）：把该用户 `review_status` 置为 `approved` 并记录 `reviewed_by`，幂等（重复调用不报错），不存在返回 404 |
| PUT | `/internal/users/{userID}/reject` | 供 admin-service 审核拒绝后调用（body: `{"reviewedBy": <管理员用户ID>}`）：把该用户 `review_status` 置为 `rejected` 并**软删除**（`deleted_at`），不存在返回 404。拒绝不可撤销——软删除后该用户不再被 `GetUserInternal`/`ApproveReviewInternal`/待审核列表查到，重复调用同一用户返回 404；软删除后允许用同一 `username`/`email` 重新注册（见下文"用户审核字段说明"） |
| GET | `/internal/tenants/{tenantCode}/db-info` | 供未来的租户服务等集群内调用方按 `tenant_code` 查询租户数据库连接信息（`dbHost`/`dbPort`/`dbName`/`dbUsername`/`dbPassword`），不存在返回 404。走 Redis cache-aside（key `sso:tenant-db-info:<tenantCode>`，TTL 60s）降低高频调用下的 MySQL 压力，Redis 故障时自动回源 MySQL 不阻断请求。**`dbPassword` 为明文**——admin-service 写入 `tenants.db_password` 时未使用 [`packages/db-credential-crypto`](../../packages/db-credential-crypto) 加密（与该包 README 描述的设计不一致，属已知技术债），本接口原样透出，调用方需自行保证响应体不落日志、不明文持久化 |

以上四个内部用户接口均不做用户角色校验，仅信任集群内可信调用方（与 `/internal/auth/verify` 同一设计），不经网关暴露。此外，均要求请求携带 `X-Internal-Token` 请求头并与配置的 `INTERNAL_API_TOKEN` 一致（见 [`middleware.RequireInternalToken`](internal/middleware/internal_token.go)），否则返回 401——`/internal/auth/verify` 不需要此密钥，它的信任边界完全依赖"仅集群内网关中间件可达"。

按规范第 16.5 章，本表接口对应的 Handler/Service 方法名均带 `Internal` 后缀（`VerifyAuthInternal`、`GetUserInternal`、`ApproveReviewInternal`、`RejectReviewInternal`、`ListUsersInternal`），与对外业务接口的方法命名区分。

角色数据每次请求都从数据库实时查询，不依赖 JWT 中的快照，权限变更（分配/移除角色）对已签发的 access token 立即生效，无需重新登录。

首个 `admin` 用户需手动在数据库中写入 `user_roles`（没有任何用户天生拥有 `admin` 角色，这是有意的引导步骤，避免自举出无法收回的初始权限）：

```sql
INSERT INTO user_roles (user_id, role_id)
SELECT <目标用户ID>, id FROM roles WHERE name = 'admin';
```

网关外部访问地址示例：`http://lead-mind-backend.dev.com/sso-service/api/v1/auth/login`

### 登出机制说明

- Access token 有效期短（默认 15 分钟），无状态校验。
- Refresh token 有效期长（默认 7 天），签发时以 `jti` 为 key 存入 Redis；登出/续期时删除对应 key，实现即时失效。
- Refresh 采用 token 轮换（rotation）：每次续期旧 refresh token 立即作废，签发新的一对 token。

### 角色模型说明

- `roles` 表存储角色定义（`name` 唯一），`user_roles` 表存储用户与角色的多对多关系
- 迁移脚本预置两个角色：`default`（新用户注册时自动分配）、`admin`（角色管理接口的访问门槛）
- 角色名不做大小写或格式约束，只要求 2-64 字符、全局唯一

### 用户审核字段说明

`users` 表额外维护开户审核状态：`review_status`（`pending`/`approved`/`rejected`，新用户注册默认 `pending`）与 `reviewed_by`（审核管理员的用户 ID，未审核时为空）。审核本身由 admin-service 的审核开户流程编排（通过后生成租户、触发建库建用户作业），本服务只被动接受 [`PUT /internal/users/{userID}/review`](#内部接口不经网关暴露)/[`PUT /internal/users/{userID}/reject`](#内部接口不经网关暴露) 调用来落地审核结果，不感知租户/作业相关的业务细节。

**拒绝审核会软删除该用户**（`deleted_at`）：`User.DeletedAt` 是普通 `*time.Time`，不是 GORM 特殊的 `gorm.DeletedAt` 类型，不会被自动注入查询过滤，因此 `FindByUsername`/`FindByEmail`/`FindByID`/`ApproveReview`/待审核列表（查 `pending`/`approved` 时）均手动加了 `deleted_at IS NULL` 条件——软删除后的用户对这些查询一律不可见，效果等同于"拒绝不可撤销"：无法再对同一 `userID` 调用 approve/reject，但允许用同一 `username`/`email` 重新走注册流程（唯一性检查同样只看未软删除的行）。例外是查询 `reviewStatus=rejected` 的待审核列表——这个场景故意不过滤 `deleted_at`，否则永远查不到任何已拒绝的用户。

### 租户解析说明（X-Tenant-Code）

`/internal/auth/verify` 返回的 `X-Tenant-Code` 头，是本服务直接查询 admin-service 拥有的 `tenants`/`user_tenants` 表得出的——两个服务共用同一个 MySQL 数据库（`sys_db`），因此这里选择建表映射直接 JOIN 查询，而不是发 HTTP 请求到 admin-service：

```sql
SELECT tenants.tenant_code
FROM user_tenants
JOIN tenants ON tenants.id = user_tenants.tenant_id
WHERE user_tenants.user_id = ? AND user_tenants.deleted_at IS NULL
  AND tenants.status = 'active' AND tenants.deleted_at IS NULL
```

见 [`TenantRepository.GetActiveTenantCodeByUserID`](internal/repository/tenant_repository.go)。只认 `status=active` 的租户——`created`（开户流程未走完）、`expired`（License 已过期）、`cancelled`（已取消）均视为"当前无有效租户"，找不到时 `VerifyAuthInternal` 不设置该响应头（不是错误，很多用户本就不属于任何租户，比如系统管理员）。这个查询与 `roles` 现有的"每次请求实时查库、不放进 JWT"设计保持一致：开户/续期/取消对下一次请求立即生效，不需要用户重新登录或刷新 token。

[`internal/model/tenant.go`](internal/model/tenant.go) 里的 `Tenant`/`UserTenant` 结构体只是这两张表的只读镜像（字段只声明本服务查询用得到的列），表结构以 admin-service 的迁移脚本为准，本服务不对这两张表做任何写操作。

## 健康检查地址

```
GET /health
```

健康检查不带路由前缀：K8s 探针直接访问 Pod，不经过网关。

## Docker 构建方式

```bash
make docker-build
```

## 部署方式

镜像发布后由 `deploy/k8s/services/sso-service/` 或 `deploy/helm/sso-service/` 中的清单部署，详见仓库根目录 [deploy/README.md](../../deploy/README.md)。

## 依赖说明

* MySQL（用户数据持久化，共用 `sys_db`）
* Redis（refresh token 存储与 access token 黑名单）
