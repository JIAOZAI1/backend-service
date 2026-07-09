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
│   └── BackendJobService.Contracts/           # ITaskHandler 插件接口，插件项目只需引用这一个项目
├── tests/
│   ├── BackendJobService.UnitTests/
│   └── BackendJobService.IntegrationTests/
├── configs/
├── migrations/                                # dotnet ef migrations 生成的迁移文件
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

make run
```

.NET 的配置系统支持用双下划线 `__` 表示嵌套 key（对应 `appsettings.json` 里的 `ConnectionStrings:MySql`），环境变量优先级高于 `appsettings.{env}.json`，敏感值不写入仓库配置文件，符合规范第 15 章。

依赖 MySQL 8.x（`backend_job_service_db`）与 RabbitMQ，需自行准备并保证 `appsettings.dev.json` 中的连接地址可达。

## 配置说明

* `appsettings.json`：全局默认值（空的连接串/密码占位）
* `appsettings.{env}.json`：环境名遵循 `dev / test / staging / prod`
* `Plugins:Directory`：插件 DLL 所在目录，相对于程序工作目录

## API 说明

服务路由前缀：`/backend-job-service`（详见根目录 [微服务项目开发规范.md](../../微服务项目开发规范.md) 第 16.3 节，网关按 host + 前缀转发且不 strip）。

Base path: `/backend-job-service/api/v1`

| Method | Path | 说明 |
| --- | --- | --- |
| POST | `/backend-job-service/api/v1/jobs` | 新建作业（配置调度类型：Cron 或一次性） |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}` | 查询作业详情 |
| POST | `/backend-job-service/api/v1/jobs/{jobId}/tasks` | 在作业下新建任务（绑定插件 HandlerType） |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}/tasks` | 查询作业下的任务列表 |
| GET  | `/backend-job-service/api/v1/jobs/{jobId}/executions` | 查询作业的执行历史（`?limit=` 默认 20，最大 200） |
| GET  | `/backend-job-service/api/v1/executions/{executionId}` | 查询单次执行详情（含每个 Task 的执行状态） |

## 可靠性机制（当前版本范围）

* **超时**：每个 Task 可配置 `TimeoutSeconds`，超时后该次尝试标记为 `TimedOut`
* **重试**：每个 Task 可配置 `MaxRetryCount`，在同一条 `TaskExecution` 记录上累加 `AttemptCount` 重试，重试次数用尽后该 Task 标记为失败，同一 Job 下后续 Task 不再执行
* **幂等**：平台不做自动幂等保证，仅在 `TaskExecutionContext.AttemptNumber` 中透传当前尝试次数，由插件 Handler 自行判断是否需要跳过已产生的副作用
* **补偿**：当前版本不提供自动补偿机制，失败执行需人工介入排查（`TaskExecution.ErrorMessage` 记录失败原因）

## 插件开发

插件项目只需引用 `BackendJobService.Contracts`，实现 `ITaskHandler`：

```csharp
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

参考实现见 [examples/SamplePlugin](../backend-job-service/examples/SamplePlugin)（`EchoTaskHandler`），可直接 `dotnet build examples/SamplePlugin -c Release` 编译后复制到 `plugins/` 联调。插件项目只引用 `BackendJobService.Contracts`，其依赖（含 `Contracts.dll` 本身）由插件加载器通过 `AssemblyDependencyResolver` 自动从插件所在目录解析，无需手动复制额外的 DLL。

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
