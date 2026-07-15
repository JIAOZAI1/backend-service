# backend-job-service

作业调度与执行服务：支持提交作业（Cron / 一次性调度）、在作业下配置有序任务（Task）、查询作业与任务的执行状态。内部包含三个逻辑模块，运行在同一个 ASP.NET Core 进程内：

* **业务服务**：REST API，负责作业/任务的创建与查询（`BackendJobService.Api` + `Application`）
* **Job Center（调度中心）**：`JobSchedulerHostedService`，定期扫描到期作业，校验后创建执行记录并投递到消息队列
* **执行 Worker**：`TaskWorkerHostedService`，消费队列消息，按 `Order` 顺序依次执行 Job 下的所有 Task

任务的具体执行逻辑以插件形式提供：每个 Task 配置一个插件程序集（`plugins/` 目录下的 DLL）和其中实现 `ITaskHandler` 接口的类型名，Worker 通过反射加载并实例化后调用。

## 目录结构

```bash
backend-job-service/
├── src/
│   ├── BackendJobService.Api/                 # 入口层：Controllers、appsettings、Program.cs
│   ├── BackendJobService.Application/         # 应用层：JobService（业务服务用例）、DTO、Cron 校验
│   ├── BackendJobService.Domain/              # 领域层：Job / JobTask / JobExecution / TaskExecution
│   ├── BackendJobService.Infrastructure/      # 基础设施层：EF Core、RabbitMQ、插件加载器、
│   │                                            #   JobSchedulerHostedService（Job Center）、
│   │                                            #   TaskWorkerHostedService（执行 Worker）
│   ├── BackendJobService.Contracts/           # ITaskHandler 插件接口 + TaskPlugin 元数据特性，
│   │                                            #   插件项目只需引用这一个项目
│   └── BackendJobService.Plugins/             # 内置插件库：MySQL 建库/建用户等运维类 Handler
├── tests/
│   ├── BackendJobService.UnitTests/
│   └── BackendJobService.IntegrationTests/
├── configs/
├── migrations/                                # 手写原生 SQL 迁移脚本（golang-migrate 风格），非 EF Core 迁移
├── plugins/                                   # 插件 DLL 放置目录，服务启动时扫描加载
├── BackendJobService.slnx
├── Dockerfile
├── Makefile
├── service.yaml
└── README.md
```

## 领域模型

```
Job (作业定义：Cron 表达式 或 一次性执行时间点)
├── JobTask[]（按 Order 排序的执行步骤，每个绑定一个插件 HandlerType）
└── 触发一次调度 → JobExecution（本次执行记录）
                    └── TaskExecution[]（每个 JobTask 对应一条，按顺序执行，
                                          前一步失败则不再执行后续步骤）
```

## 本地启动方式

```bash
export ConnectionStrings__MySql="Server=192.168.8.184;Port=3306;Database=backend_job_service_db;User=backend_job_service_user;Password=xxx;"
export RabbitMq__Host=192.168.8.184
export RabbitMq__Port=30672
export RabbitMq__Username=admin
export RabbitMq__Password=xxx
export Internal__Token=xxx
export JOB_PLUGIN_ADMIN_SERVICE_BASE_URL=http://localhost:5080   # admin-service 本地地址
export JOB_PLUGIN_SSO_SERVICE_BASE_URL=http://localhost:5090     # sso-service 本地地址

make run
```

.NET 的配置系统支持用双下划线 `__` 表示嵌套 key（对应 `appsettings.json` 里的 `ConnectionStrings:MySql`），环境变量优先级高于 `appsettings.{env}.json`，敏感值不写入仓库配置文件，符合规范第 15 章。

依赖 MySQL 8.x（`backend_job_service_db`）与 RabbitMQ，需自行准备并保证 `appsettings.dev.json` 中的连接地址可达。

## 配置说明

* `appsettings.json`：全局默认值（空的连接串/密码占位）
* `appsettings.{env}.json`：环境名遵循 `dev / test / staging / prod`
* `Plugins:Directory`：插件 DLL 所在目录，相对于程序工作目录
* `Internal:Token`：集群内服务间调用共享密钥，既用于校验入站的 `POST /jobs`/`POST /jobs/{jobId}/tasks`（见下文"内部调用鉴权"），也用于本服务插件反向调用 admin-service/sso-service 内部接口时携带的 `X-Internal-Token`，未配置时启动直接抛异常
* `JOB_PLUGIN_ADMIN_SERVICE_BASE_URL` / `JOB_PLUGIN_SSO_SERVICE_BASE_URL`：内置插件反查 admin-service/sso-service 内部接口时使用的 base URL（集群内 Service DNS，非敏感信息），见下文"内置插件"

## 数据库迁移

迁移脚本是手写的原生 SQL（`migrations/*.up.sql` / `*.down.sql`），不使用 EF Core 自带的迁移机制（`dotnet ef migrations`）——EF Core 仅用作运行时 ORM（查询/写入），schema 变更统一走 SQL 脚本，与仓库里 Go 服务的迁移方式保持一致，跨语言服务用同一套迁移工具（[golang-migrate](https://github.com/golang-migrate/migrate)）管理。

```bash
export MYSQL_PASSWORD=xxx
make migrate-up      # 应用所有未执行的迁移
make migrate-down     # 回滚最近一次迁移
```

新增表结构变更时，在 `migrations/` 下按 `NNNNNN_description.up.sql` / `.down.sql` 命名新增一对文件（序号递增），同时手动同步更新 `Infrastructure/Persistence/Configurations/` 下对应的 EF Core 实体配置，保证运行时 ORM 模型与实际表结构一致——两者不再由同一份迁移文件自动保证同步，这是放弃 EF Core 迁移机制的代价，修改 schema 时需要格外注意两处一起改。

## API 说明

服务路由前缀：`/backend-job-service`（详见根目录 [微服务项目开发规范.md](../../微服务项目开发规范.md) 第 16.3 节，网关按 host + 前缀转发且不 strip）。

Base path: `/backend-job-service/api/v1`

| Method | Path | 说明 |
| --- | --- | --- |
| POST | `/backend-job-service/api/v1/jobs` | 新建作业（配置调度类型：Cron 或一次性） |
| GET  | `/backend-job-service/api/v1/jobs` | 分页查询作业列表（`?page=` 默认 1，`?pageSize=` 默认 20，最大 200，按 id 倒序） |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}` | 查询作业详情 |
| POST | `/backend-job-service/api/v1/jobs/{jobId}/tasks` | 在作业下新建任务（绑定插件 HandlerType） |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}/tasks` | 查询作业下的任务列表 |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}/executions` | 查询作业的执行历史（`?limit=` 默认 20，最大 200） |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}/status` | 查询作业状态聚合视图（作业状态 + 最近一次执行及其 Task 状态），供前端轮询 |
| GET  | `/backend-job-service/api/v1/executions/{executionId}` | 查询单次执行详情（含每个 Task 的执行状态） |

标 🔒 的两个写接口（`POST /jobs`、`POST /jobs/{jobId}/tasks`）是本服务对外的公开业务 API，前端经网关正常调用；同时也会触发建库/建用户等运维操作，因此对未经网关登录校验的调用方（如集群内直连）额外要求 `X-Internal-Token` 请求头与配置的 `Internal:Token` 一致，见下文"内部调用鉴权"。

## 内部调用鉴权

`POST /jobs` 与 `POST /jobs/{jobId}/tasks` 是公开业务接口（规范 16.5.3：作业/任务创建不算"仅内部调用接口"，即使目前主要调用方之一是 admin-service 的审核开户流程，触发 `mysql-create-database`/`mysql-create-user` 建库建用户，路由与方法命名也维持对外 API 规范），前端经网关调用時不要求携带内部密钥。这两个接口不经网关暴露给集群内直连调用时，仅靠"网络可达性"作为信任边界不足以防止集群内其他 Pod 越权调用，因此按调用来源分两种校验方式：

* [`RequireInternalTokenMiddleware`](src/BackendJobService.Api/Auth/RequireInternalTokenMiddleware.cs) 只拦截上述两个写接口（按 method + path 精确匹配），其余只读接口维持现状不做改动
* 请求带有网关注入的 `X-User-Id`/`X-Username`（见 [`GatewayUser`](src/BackendJobService.Api/Auth/GatewayUser.cs)，客户端无法伪造）时视为已登录用户经网关调用，直接放行
* 否则要求携带 `X-Internal-Token` 请求头并与配置的 `Internal:Token` 一致，用于集群内直连调用（如 admin-service）
* 密钥通过 `Internal:Token`（环境变量 `Internal__Token`）配置，与 sso-service、admin-service 共用同一份（`config-dev-secret` 的 `internal-api-token`）
* 校验用固定时间比较（`CryptographicOperations.FixedTimeEquals`）防止时序侧信道泄露密钥
* 未配置 `Internal:Token` 时服务启动直接抛异常，不会以"不校验"的状态误上线

## 可靠性机制（当前版本范围）

* **超时**：每个 Task 可配置 `TimeoutSeconds`，超时后该次尝试标记为 `TimedOut`
* **重试**：每个 Task 可配置 `MaxRetryCount`，在同一条 `TaskExecution` 记录上累加 `AttemptCount` 重试，重试次数用尽后该 Task 标记为失败，同一 Job 下后续 Task 不再执行
* **幂等**：平台不做自动幂等保证，仅在 `TaskExecutionContext.AttemptNumber` 中透传当前尝试次数，由插件 Handler 自行判断是否需要跳过已产生的副作用
* **补偿**：当前版本不提供自动补偿机制，失败执行需人工介入排查（`TaskExecution.ErrorMessage` 记录失败原因）

## 插件开发

插件项目只需引用 `BackendJobService.Contracts`，实现 `ITaskHandler`，并用 `TaskPlugin` 特性声明插件元数据（名称/描述/版本/作者，宿主加载插件时会读取并打印到日志）：

```csharp
[TaskPlugin("my-task", Description = "插件用途一句话描述", Version = "1.0.0")]
public class MyTaskHandler : ITaskHandler
{
    public async Task<TaskResult> ExecuteAsync(TaskExecutionContext context, CancellationToken cancellationToken)
    {
        // 反序列化 context.ParametersJson，执行业务逻辑
        return TaskResult.Ok();
    }
}
```

编译产出的 DLL 放入 `plugins/` 目录，创建 JobTask 时填写 `pluginAssembly`（DLL 文件名）与 `handlerType`（含命名空间的完整类型名）。**新增/替换插件需要重启服务生效**（固定目录扫描加载，非热加载）。

`OutputJson` 必须是合法 JSON——`task_executions.output_json` 是数据库原生 JSON 列，写入非法 JSON 会导致保存失败；Worker 侧对此做了防御性兜底（非法 JSON 会被包一层降级保存并标注警告），但插件应自己保证输出合法。

参考实现见 [examples/SamplePlugin](../backend-job-service/examples/SamplePlugin)（`EchoTaskHandler`），可直接 `dotnet build examples/SamplePlugin -c Release` 编译后复制到 `plugins/` 联调。插件项目只引用 `BackendJobService.Contracts`，其依赖（含 `Contracts.dll` 本身）由插件加载器通过 `AssemblyDependencyResolver` 自动从插件所在目录解析，无需手动复制额外的 DLL。带 NuGet 依赖的插件项目需设置 `<EnableDynamicLoading>true</EnableDynamicLoading>` 并用 `dotnet publish` 产出（依赖 DLL 会一起进输出目录）。

### 内置插件（BackendJobService.Plugins）

`src/BackendJobService.Plugins` 是随服务维护的内置插件库，Docker 镜像构建时会发布到 `/app/plugins`；本地联调执行 `make publish-plugins` 发布到 `plugins/` 目录。`pluginAssembly` 填 `BackendJobService.Plugins.dll`。

| Handler（handlerType） | 说明 | parameters_json |
| --- | --- | --- |
| `BackendJobService.Plugins.MySql.CreateDatabaseHandler` | 创建数据库（幂等，已存在则跳过） | `databaseInstanceId`（必填）、`databaseName`（必填）、`charset`（默认 utf8mb4）、`collation`（可选） |
| `BackendJobService.Plugins.MySql.CreateUserHandler` | 创建用户并可选授权（幂等，已存在则跳过且不改密码） | `databaseInstanceId`（必填）、`username`/`password`（必填）、`host`（默认 `%`）、`grantDatabase`（可选）、`privileges`（默认 `["ALL PRIVILEGES"]`，白名单校验） |
| `BackendJobService.Plugins.Sso.MarkUserReviewedHandler` | 调用 sso-service 标记用户已审核 | `userId`/`reviewedBy`（必填） |
| `BackendJobService.Plugins.Admin.ActivateTenantHandler` | 调用 admin-service 把租户状态置为 Active | `tenantId`（必填，`Tenant.TenantId` 业务键） |
| `BackendJobService.Plugins.Admin.ExpireOverdueTenantsHandler` | 调用 admin-service 批量检查并过期到期租户，不针对单个租户 | 无（`parameters_json` 恒为 `"{}"`） |

**MySQL 管理员连接串不再由部署环境固定注入**：`CreateDatabaseHandler`/`CreateUserHandler` 按 `databaseInstanceId` 调用 admin-service 的 `GET /internal/database-instances/{id}/credentials` 现取解密后的连接信息（host/port/username/password），现取现用、不落库、不缓存，拼成 MySqlConnector DSN 后再连接——这样同一个插件可以对接管理员在 admin-service 注册的任意数据库实例，不再局限于单一固定实例。`Sso.MarkUserReviewedHandler`/`Admin.ActivateTenantHandler`/`Admin.ExpireOverdueTenantsHandler` 同样通过反查其他服务的内部接口完成动作，而非直接操作数据库。

`ExpireOverdueTenantsHandler` 是每日租户 License 监控的执行体：挂在一个**全局 Cron Job**下（`scheduleType: Cron`，如 `cronExpression: "0 2 * * *"` 每天 02:00 UTC），与其余插件挂在"每个租户开户时创建的一次性 Job"下不同——这个 Job 只需要**手动创建一次**（`POST /backend-job-service/api/v1/jobs` + `POST .../jobs/{jobId}/tasks`，运维操作，不在代码里自动创建，因为本服务的 `jobs.name` 没有唯一约束、也没有按名称查重的接口，重复调用创建会产生重复 Job），此后由 `JobSchedulerHostedService` 按 `cronExpression` 永久自动重复触发，不需要每天重建。

四个 HTTP 类内置插件（Sso/Admin 目录下）调用 admin-service/sso-service 时使用的 base URL 与共享密钥统一从环境变量读取，见 [`InternalServiceClientHelper`](src/BackendJobService.Plugins/Internal/InternalServiceClientHelper.cs)：`JOB_PLUGIN_ADMIN_SERVICE_BASE_URL`/`JOB_PLUGIN_SSO_SERVICE_BASE_URL` 给出目标服务的 Service DNS，`Internal__Token`（与本服务入站鉴权用的同一个环境变量）作为 `X-Internal-Token` 请求头附加到每个出站请求上。插件运行在 `Activator.CreateInstance` 构造的对象里，没有 DI 容器，因此 `HttpClient` 是手动构造的，不经过 `IHttpClientFactory`。

安全约定：库名/用户名/host/权限均做白名单校验（非法即失败，不拼接进 SQL）；新用户密码由调用方生成并自行保管，执行结果 `output_json` 不回显密码；数据库实例的管理员密码只在插件执行期间的内存中短暂持有，不落库、不写入 `output_json`。

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

镜像发布后由 `deploy/k8s/services/backend-job-service/` 中的清单部署，详见仓库根目录 [deploy/README.md](../../deploy/README.md)。

## 依赖说明

* MySQL（作业/任务/执行记录持久化，数据库名 `backend_job_service_db`）
* RabbitMQ（Job Center 与 Worker 之间的任务分发队列）
