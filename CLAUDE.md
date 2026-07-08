# CLAUDE.md

本文件为 Claude Code 在本仓库工作时的上下文说明。

## 仓库现状

当前仓库仅包含一份规范文档 [微服务项目开发规范.md](微服务项目开发规范.md)，尚未创建任何 `services/`、`packages/`、`deploy/` 等实际目录/代码。规范文档描述的是**目标结构**，不是当前状态——新建目录或服务前，先按规范搭建骨架，不要假设这些目录已存在。

## 规范文档速查

[微服务项目开发规范.md](微服务项目开发规范.md) 是本仓库唯一的权威规范来源，涵盖：

| 章节 | 内容 |
| --- | --- |
| 1-2 | 规范目标、总体原则（Monorepo、分层、kebab-case 命名、服务自治） |
| 3-6 | 根目录结构、目录/服务命名规范（`<domain>-service`） |
| 7 | **多语言服务规范**：Go / Node.js-TypeScript / Java / Python / .NET Core 五种语言的目录结构、分层依赖方向、命名风格、配置注入、数据访问、测试规范、Makefile 目标 |
| 8-9 | 文件命名规范、`service.yaml` 服务元信息 |
| 10-13 | 公共包规范（`packages/`）、脚本规范（`scripts/`）、部署目录规范（`deploy/`） |
| 14-15 | 环境命名（仅 `dev/test/staging/prod`）、配置管理与敏感信息禁止事项 |
| 16-17 | API 规范（`/api/v1/...` + `/health`）、数据库命名规范 |
| 18-19 | Git 分支规范、Conventional Commits 提交规范 |
| 20-22 | Makefile 规范、文档规范、新服务接入最小标准 |
| 23-26 | 推荐最终目录树、团队强制约定清单、落地建议、结语 |

## 对 Claude Code 的强制约束

在本仓库创建或修改任何内容时，必须遵循以下规则（详见规范原文对应章节）：

1. **目录/服务命名**统一 kebab-case，服务名格式为 `<domain>-service`（第 4、6 章）。
2. **新建服务**必须放在 `services/<domain>-service/`，并按第 7 章对应语言小节的目录结构、分层依赖方向搭建，同时提供 `Dockerfile`、`Makefile`、`README.md`、`service.yaml`、健康检查接口（第 5、7、9、22 章）。
3. **分层依赖方向不可反向**——例如 .NET Core 的 `Api → Application → Domain`、Go 的 `handler → service → repository`，各语言具体方向见第 7 章对应子节。
4. **配置与密钥**：环境名只用 `dev/test/staging/prod`；禁止把生产密钥、密码、Token 提交到仓库或硬编码（第 14、15 章）。
5. **API**：REST 路径为 `/api/{version}/{resource}`，必须提供 `/health`（第 16 章）。
6. **数据库**：库名 `<domain>_db`，表名小写复数+下划线，字段含 `id/created_at/updated_at/deleted_at`（第 17 章）。
7. **Git**：分支遵循 `feature/`、`fix/`、`hotfix/`、`release/` 前缀；提交信息遵循 Conventional Commits（`feat/fix/docs/refactor/test/chore/ci`）（第 18-19 章）。
8. **公共代码**只能放在 `packages/`，禁止服务间直接互相引用内部代码（第 2.2、10 章）。
9. **脚本**统一放在 `scripts/`，命名为“动词-对象.sh”（第 11-12 章）。

## 修改规范文档本身时

若用户要求调整规范内容（如新增语言支持、调整命名规则），修改前先读取现有章节结构和风格（各语言小节已统一为：目录结构 → 分层依赖方向 → 命名与代码风格 → 配置与依赖注入 → 数据访问 → 测试 → Makefile 目标），新增内容应保持同等细节深度和格式一致性。
