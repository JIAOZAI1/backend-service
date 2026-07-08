# backend-service

微服务 Monorepo，遵循 [微服务项目开发规范.md](微服务项目开发规范.md)。

## 目录结构

```bash
.
├── services/                  # 微服务目录
├── packages/                  # 公共包 / SDK / 基础库
├── deploy/                    # 部署配置
├── scripts/                   # 开发 / 构建 / 运维脚本
├── configs/                   # 全局配置模板
├── docs/                      # 项目文档
├── tests/                     # 跨服务集成测试 / E2E
├── tools/                     # 开发工具链配置
├── .github/                   # GitHub Actions / 模板等
├── Makefile                   # 仓库级统一命令入口
├── README.md
└── CONTRIBUTING.md
```

## 新增服务

新服务放在 `services/<domain>-service/`，最小标准见 [docs/service-template.md](docs/service-template.md)。

## 规范

详见 [微服务项目开发规范.md](微服务项目开发规范.md)。
