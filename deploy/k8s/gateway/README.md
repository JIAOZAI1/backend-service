# backend-gateway

统一后端服务网关，基于 k3s 内置的 Traefik Ingress Controller，通过标准 K8s `Ingress` 资源实现。

## 转发规则

按 **host + 路由前缀** 匹配，转发到对应服务的 K8s Service，**不 strip 前缀**（请求路径原样转发给后端，后端服务内部路由需自带相同前缀）。规则详见 [微服务项目开发规范.md](../../../微服务项目开发规范.md) 第 16.3 节。

| Host | 说明 |
| --- | --- |
| `lead-mind-backend.dev.com` | dev 环境统一入口 |

完整的路由前缀 ↔ 服务映射见 [docs/route-mapping.md](../../../docs/route-mapping.md)。

[ingress.yaml](ingress.yaml) 拆分为两个 Ingress：

| Ingress | 中间件 | 路由 |
| --- | --- | --- |
| `backend-gateway-public` | CORS | `/sso-service`（登录/注册等必须匿名可达，服务内部自带 `RequireAuth`） |
| `backend-gateway-protected` | CORS + 登录校验 | `/backend-job-service` 及所有后续业务服务 |

## 登录校验（ForwardAuth）

受保护路由统一在网关层校验登录 token（[auth-middleware.yaml](auth-middleware.yaml)，`Middleware` 资源 `gateway-auth`）：

1. 网关把每个请求的头转发给 sso-service 的集群内部端点 `GET /internal/auth/verify`；
2. sso-service 复用自身逻辑做 JWT 验签 + Redis 黑名单检查（登出即时生效）；
3. 校验通过（204）时，网关**删除客户端自带的同名头**，把校验端点返回的 `X-User-Id`/`X-Username`/`X-User-Roles`/`X-Tenant-Code` 写入原请求转发给后端——后端服务直接信任这些头，无需解析 JWT、不持有 JWT 密钥；`X-User-Roles` 为逗号分隔的角色名列表，`X-Tenant-Code` 是当前用户所属 active 租户的 tenant_code（sso-service 直接查 admin-service 拥有的 tenants/user_tenants 表得出，两个服务共用同一个 MySQL 数据库，不经 HTTP 调用；用户未开户或租户非 active 状态时该头缺失，下游服务需自行处理），二者均每次请求实时查库生成，不依赖 JWT 快照，角色变更/开户/租户状态变化立即生效；
4. 校验失败时，sso-service 的 401 响应原样返回给客户端。

注意：这些头只有**经网关转发**的请求才可信；集群内直连服务不经过校验，应通过 NetworkPolicy 或访问约定禁止绕过网关调用业务服务。

## CORS

跨域策略统一在网关层配置（[cors-middleware.yaml](cors-middleware.yaml)，`Middleware` 资源 `gateway-cors`），通过 `traefik.ingress.kubernetes.io/router.middlewares` 注解挂载到 Ingress。所有经网关转发的服务共用同一份 CORS 策略，**服务自身不再重复处理 CORS**——两处都加会导致响应重复 `Access-Control-Allow-Origin` 头，浏览器会拒绝。

当前允许的来源（`accessControlAllowOriginList`，以 [cors-middleware.yaml](cors-middleware.yaml) 实际内容为准）：

* `http://localhost:3000`（本地前端开发）
* `http://localhost:5173`（本地前端开发，Vite 默认端口）
* `http://lead-mind-backend.dev.com`
* `http://lead-mind.dev.com`

新增允许的来源时，编辑 [cors-middleware.yaml](cors-middleware.yaml) 后重新 `kubectl apply`。

## 新增服务接入

在 [ingress.yaml](ingress.yaml) 中 **`backend-gateway-protected`** 的 `spec.rules[0].http.paths` 下追加一条（默认所有新服务都需要登录；确需匿名访问的路由才加到 `backend-gateway-public`）：

```yaml
- path: /<domain>-service
  pathType: Prefix
  backend:
    service:
      name: <domain>-service
      port:
        name: http
```

同时更新 [docs/route-mapping.md](../../../docs/route-mapping.md)。新服务从 `X-User-Id`/`X-Username`/`X-User-Roles`/`X-Tenant-Code` 请求头读取当前用户、角色、所属租户（参考 backend-job-service 的 `GatewayUser`、admin-service 的 `GatewayUser` + `RequireAdminRole`），无需自行解析 JWT；`X-Tenant-Code` 可能缺失（用户未开户或租户非 active），读取前需判空。

## 部署

```bash
kubectl apply -f deploy/k8s/gateway/cors-middleware.yaml
kubectl apply -f deploy/k8s/gateway/auth-middleware.yaml
kubectl apply -f deploy/k8s/gateway/ingress.yaml

# 首次从单一 backend-gateway 迁移到 public/protected 拆分时，
# 必须删除旧 Ingress，否则旧的无鉴权路由仍然生效：
kubectl delete ingress backend-gateway -n default --ignore-not-found
```

## 本地验证（dev，未配置真实 DNS 时）

```bash
curl -H "Host: lead-mind-backend.dev.com" http://192.168.8.184/sso-service/api/v1/auth/login \
  -X POST -H "Content-Type: application/json" \
  -d '{"username":"x","password":"y"}'
```

角色管理接口需要 `admin` 角色，完整接口列表见 [sso-service README](../../../services/sso-service/README.md#api-说明)。
