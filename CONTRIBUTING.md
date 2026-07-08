# CONTRIBUTING

本文件为团队强制约定，摘自 [微服务项目开发规范.md](微服务项目开发规范.md) 第 24 章，PR Review 时按此检查。

1. 所有目录统一使用 `kebab-case`
2. 所有业务服务必须放在 `services/`
3. 所有公共包必须放在 `packages/`
4. 服务命名必须使用 `<domain>-service`
5. 每个服务必须包含 `README.md`、`Dockerfile`、`Makefile`、`service.yaml`
6. 所有脚本必须放在 `scripts/`
7. 脚本命名必须使用“动词-对象”格式（如 `build-image.sh`）
8. 环境命名只允许 `dev / test / staging / prod`
9. 所有服务必须提供 `/health`
10. API 必须使用 `/api/v1/...`
11. 提交信息必须符合 [Conventional Commits](https://www.conventionalcommits.org/)（`feat/fix/docs/style/refactor/test/chore/ci`）
12. 禁止提交生产密钥、密码、Token、证书等敏感信息
13. 新服务上线前必须补齐 README、基础测试、部署配置和服务元信息

## 分支规范

`main`、`develop`、`feature/<name>`、`fix/<name>`、`hotfix/<name>`、`release/<version>`

## Code Review 检查项

* 是否符合目录规范
* 是否符合命名规范
* 是否补齐文档
* 是否包含健康检查与配置模板
* 是否影响公共规范
