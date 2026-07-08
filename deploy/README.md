# deploy

部署相关配置统一放在本目录。

| 子目录 | 用途 |
| --- | --- |
| `docker/` | 基础镜像、共享 Docker 配置 |
| `docker-compose/` | 本地/联调/调试环境编排（`local`、`integration`、`debug`） |
| `k8s/` | Kubernetes 清单（`base`、`services/<domain>-service`、`environments/<env>`） |
| `helm/` | Helm Chart（按服务 + `platform`） |
| `terraform/` | 基础设施即代码 |

环境名仅允许 `dev / test / staging / prod`，详见根目录 [微服务项目开发规范.md](../微服务项目开发规范.md) 第 13-14 章。
