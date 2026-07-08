# backend-gateway

统一后端服务网关，基于 k3s 内置的 Traefik Ingress Controller，通过标准 K8s `Ingress` 资源实现。

## 转发规则

按 **host + 路由前缀** 匹配，转发到对应服务的 K8s Service，**不 strip 前缀**（请求路径原样转发给后端，后端服务内部路由需自带相同前缀）。规则详见 [微服务项目开发规范.md](../../../微服务项目开发规范.md) 第 16.3 节。

| Host | 说明 |
| --- | --- |
| `lead-mind-backend.dev.com` | dev 环境统一入口 |

完整的路由前缀 ↔ 服务映射见 [docs/route-mapping.md](../../../docs/route-mapping.md)。

## CORS

跨域策略统一在网关层配置（[cors-middleware.yaml](cors-middleware.yaml)，`Middleware` 资源 `gateway-cors`），通过 `traefik.ingress.kubernetes.io/router.middlewares` 注解挂载到 Ingress。所有经网关转发的服务共用同一份 CORS 策略，**服务自身不再重复处理 CORS**——两处都加会导致响应重复 `Access-Control-Allow-Origin` 头，浏览器会拒绝。

当前允许的来源（`accessControlAllowOriginList`，以 [cors-middleware.yaml](cors-middleware.yaml) 实际内容为准）：

* `http://localhost:3000`（本地前端开发）
* `http://localhost:5173`（本地前端开发，Vite 默认端口）
* `http://lead-mind-backend.dev.com`
* `http://lead-mind.dev.com`

新增允许的来源时，编辑 [cors-middleware.yaml](cors-middleware.yaml) 后重新 `kubectl apply`。

## 新增服务接入

在 [ingress.yaml](ingress.yaml) 的 `spec.rules[0].http.paths` 下追加一条：

```yaml
- path: /<domain>-service
  pathType: Prefix
  backend:
    service:
      name: <domain>-service
      port:
        name: http
```

同时更新 [docs/route-mapping.md](../../../docs/route-mapping.md)。

## 部署

```bash
kubectl apply -f deploy/k8s/gateway/cors-middleware.yaml
kubectl apply -f deploy/k8s/gateway/ingress.yaml
```

## 本地验证（dev，未配置真实 DNS 时）

```bash
curl -H "Host: lead-mind-backend.dev.com" http://192.168.8.184/sso-service/api/v1/auth/login \
  -X POST -H "Content-Type: application/json" \
  -d '{"username":"x","password":"y"}'
```

角色管理接口需要 `admin` 角色，完整接口列表见 [sso-service README](../../../services/sso-service/README.md#api-说明)。
