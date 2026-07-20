# 路由映射文档

记录 K8s 服务网关的 host、路由前缀与后端微服务的对应关系。每新增一个通过网关暴露的服务，必须在本文档追加一行，并同步更新网关的 Ingress 资源（见 [deploy/k8s/gateway/](../deploy/k8s/gateway/)）。

规则详见 [微服务项目开发规范.md](../微服务项目开发规范.md) 第 16.3 节。网关按 **host + 路由前缀** 转发，**不 strip 前缀**，请求原样转发到后端服务，服务内部路由必须自带相同前缀。

## dev 环境

| Host | 路由前缀 | 后端服务 | K8s Service | 网关登录校验 | 说明 |
| --- | --- | --- | --- | --- | --- |
| `lead-mind-backend.dev.com` | `/sso-service` | sso-service | `sso-service.default.svc.cluster.local` | 否（服务内部自带 `RequireAuth`） | 统一登录服务（注册/登录/注销/续期/角色管理），详见 [sso-service README](../services/sso-service/README.md#api-说明) |
| `lead-mind-backend.dev.com` | `/backend-job-service` | backend-job-service | `backend-job-service.default.svc.cluster.local` | 是 | 作业调度与执行服务（Job/Task 管理、执行状态查询），详见 [backend-job-service README](../services/backend-job-service/README.md#api-说明) |
| `lead-mind-backend.dev.com` | `/admin-service` | admin-service | `admin-service.default.svc.cluster.local` | 是 | 管理员服务（系统级设置、用户注册审核开户、数据库实例注册），所有接口要求 `admin` 角色，详见 [admin-service README](../services/admin-service/README.md#api-说明) |
| `lead-mind-backend.dev.com` | `/ai-agent` | ai-agent | `ai-agent.default.svc.cluster.local` | 是 | AI Agent 服务（对话/工具调用），代码在独立仓库 [lead-mind-ai-agent](https://github.com/JIAOZAI1/lead-mind-ai-agent)（非本 monorepo，不受本仓库规范约束）；K8s Service 名与内部路由前缀均为 `ai-agent`，非 `<domain>-service` 格式，是已知的命名例外；服务当前尚未接入网关注入的 `X-User-Id` 等身份头，仅读取自定义 `tenant_id` 头做路由（非鉴权），接入方需自行改造后才能使用用户身份 |

「网关登录校验」为 **是** 的服务由网关 ForwardAuth 统一校验 access token（JWT 验签 + 登出黑名单，见 [deploy/k8s/gateway/README.md](../deploy/k8s/gateway/README.md#登录校验forwardauth)），校验通过后网关向后端注入 `X-User-Id`/`X-Username`/`X-User-Roles`/`X-Tenant-Code` 请求头（`X-Tenant-Code` 为当前用户所属 active 租户的 tenant_code，用户未开户/租户非 active 状态时该头缺失，下游服务需自行处理缺失场景）。

## 新增服务接入步骤

1. 服务内部路由按 `/<domain>-service/api/{version}/{resource}` 注册（网关不 strip 前缀）
2. 在上表追加一行：host、前缀、服务名、K8s Service DNS、是否网关登录校验
3. 在 [deploy/k8s/gateway/ingress.yaml](../deploy/k8s/gateway/ingress.yaml) 中新增一条 `path` 规则，指向新服务的 K8s Service——默认加到 `backend-gateway-protected`（自动获得登录校验），确需匿名访问才加到 `backend-gateway-public`
4. 需要用户身份时从网关注入的 `X-User-Id`/`X-Username`/`X-User-Roles`/`X-Tenant-Code` 请求头读取，不要在服务内解析 JWT；`X-User-Roles` 为逗号分隔的角色名列表，`X-Tenant-Code` 可能缺失（用户未开户或租户非 active），仅经网关转发的请求可信
5. 提交 PR，Review 时对照本文档核对前缀与 Ingress 规则是否一致
