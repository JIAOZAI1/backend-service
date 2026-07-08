# sso-service

统一 JWT 登录服务，提供注册、登录、注销、Token 续期能力，供其他微服务作为统一身份认证入口。

## 目录结构

```bash
sso-service/
├── cmd/server/            # 程序入口（main.go）
├── internal/
│   ├── handler/           # HTTP 入口，参数解析与响应封装
│   ├── service/            # 业务逻辑编排（注册/登录/续期/注销）
│   ├── repository/         # 数据访问：MySQL(用户) + Redis(refresh token/黑名单)
│   ├── model/               # 领域模型 / DTO
│   ├── config/               # 配置加载（viper）
│   └── middleware/            # JWT 鉴权中间件
├── pkg/jwtutil/            # JWT 签发与解析，可被其他服务安全引用
├── tests/                 # 集成测试
├── configs/                # 服务级配置模板（app.<env>.yaml）
├── migrations/              # 数据库迁移脚本
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
export SSO_APP_ENV=dev   # 对应 configs/app.dev.yaml

make run
```

依赖 MySQL 8.x（`sso_db`）与 Redis 6.x，需自行准备并保证 `configs/app.dev.yaml` 中的连接地址可达。

## 配置说明

配置模板位于 `configs/app.<env>.yaml`，环境仅允许 `dev / test / staging / prod`。

## 环境变量说明

| 变量 | 说明 |
| --- | --- |
| `SSO_APP_ENV` | 运行环境，决定加载哪个 `app.<env>.yaml`，默认 `dev` |
| `MYSQL_PASSWORD` | MySQL 密码，注入到 DSN 中 |
| `REDIS_PASSWORD` | Redis 密码 |
| `JWT_SECRET` | JWT 签名密钥，生产环境必须通过 Secret 管理注入，禁止硬编码 |

## API 说明

服务路由前缀：`/sso-service`（详见仓库根目录 [docs/route-convention.md](../../docs/route-convention.md)，网关按 host + 前缀转发且不 strip，因此服务内部路由必须自带此前缀）。

Base path: `/sso-service/api/v1/auth`

| Method | Path | 说明 |
| --- | --- | --- |
| POST | `/sso-service/api/v1/auth/register` | 注册新用户 |
| POST | `/sso-service/api/v1/auth/login` | 登录，返回 access/refresh token |
| POST | `/sso-service/api/v1/auth/refresh` | 使用 refresh token 换取新的 token 对（旧 refresh token 立即失效） |
| POST | `/sso-service/api/v1/auth/logout` | 注销，使 refresh token 失效 |
| GET  | `/sso-service/api/v1/auth/me` | 需 `Authorization: Bearer <access token>`，返回当前用户信息 |

网关外部访问地址示例：`http://lead-mind-backend.dev.com/sso-service/api/v1/auth/login`

### 登出机制说明

- Access token 有效期短（默认 15 分钟），无状态校验。
- Refresh token 有效期长（默认 7 天），签发时以 `jti` 为 key 存入 Redis；登出/续期时删除对应 key，实现即时失效。
- Refresh 采用 token 轮换（rotation）：每次续期旧 refresh token 立即作废，签发新的一对 token。

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

* MySQL（用户数据持久化，数据库名 `sso_db`）
* Redis（refresh token 存储与 access token 黑名单）
