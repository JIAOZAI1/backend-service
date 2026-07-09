using System.Text;
using System.Text.Json;
using BackendJobService.Application.Interfaces;
using BackendJobService.Contracts;
using BackendJobService.Domain.Entities;
using BackendJobService.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BackendJobService.Infrastructure.Scheduling;

/// <summary>
/// 执行 Worker：消费队列里的 JobExecution 触发消息，按 Order 顺序依次执行该 Job 下的
/// 所有 Task，每个 Task 通过反射加载插件 DLL 执行。对应架构描述里的“c) 执行 worker”
/// 与“d) 反射实例化任务 DLL”。
/// </summary>
public class TaskWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<TaskWorkerHostedService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _rabbitOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var messageJson = Encoding.UTF8.GetString(args.Body.ToArray());
            try
            {
                var message = JsonSerializer.Deserialize<TaskExecutionMessage>(messageJson)
                    ?? throw new InvalidOperationException("empty message body");

                await ProcessJobExecutionAsync(message.JobExecutionId, stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message: {Message}", messageJson);
                // 基础版：处理失败直接 requeue=false，避免毒消息无限循环；重试逻辑发生在
                // Task 级别（见 ExecuteTaskWithRetryAsync），消息级别失败视为需要人工介入。
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _rabbitOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // 保持后台服务存活，直到宿主关闭
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task ProcessJobExecutionAsync(long jobExecutionId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var executionRepository = scope.ServiceProvider.GetRequiredService<IJobExecutionRepository>();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var handlerLoader = scope.ServiceProvider.GetRequiredService<ITaskHandlerLoader>();

        var execution = await executionRepository.GetByIdAsync(jobExecutionId, cancellationToken)
            ?? throw new InvalidOperationException($"job execution {jobExecutionId} not found");

        var job = await jobRepository.GetWithTasksByIdAsync(execution.JobId, cancellationToken)
            ?? throw new InvalidOperationException($"job {execution.JobId} not found");

        execution.Status = JobExecutionStatus.Running;
        execution.StartedAt = DateTime.UtcNow;
        await executionRepository.SaveChangesAsync(cancellationToken);

        var orderedTasks = job.Tasks.OrderBy(t => t.Order).ToList();
        var allSucceeded = true;
        string? firstError = null;

        foreach (var task in orderedTasks)
        {
            var taskExecution = new TaskExecution
            {
                JobExecutionId = execution.Id,
                JobTaskId = task.Id,
                Status = TaskExecutionStatus.Pending,
            };
            await executionRepository.AddTaskExecutionAsync(taskExecution, cancellationToken);

            var success = await ExecuteTaskWithRetryAsync(task, execution, taskExecution, handlerLoader, executionRepository, cancellationToken);

            if (!success)
            {
                allSucceeded = false;
                firstError ??= taskExecution.ErrorMessage;
                // 按顺序步骤执行：前一步失败则不再继续后续步骤
                break;
            }
        }

        execution.Status = allSucceeded ? JobExecutionStatus.Succeeded : JobExecutionStatus.Failed;
        execution.FinishedAt = DateTime.UtcNow;
        execution.ErrorMessage = firstError;
        await executionRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ExecuteTaskWithRetryAsync(
        JobTask task,
        JobExecution execution,
        TaskExecution taskExecution,
        ITaskHandlerLoader handlerLoader,
        IJobExecutionRepository executionRepository,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, task.MaxRetryCount + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            taskExecution.Status = TaskExecutionStatus.Running;
            taskExecution.AttemptCount = attempt;
            taskExecution.StartedAt = DateTime.UtcNow;
            await executionRepository.SaveChangesAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(task.TimeoutSeconds));

            try
            {
                var handler = handlerLoader.CreateHandler(task.PluginAssembly, task.HandlerType);
                var context = new TaskExecutionContext
                {
                    JobId = task.JobId,
                    JobTaskId = task.Id,
                    JobExecutionId = execution.Id,
                    TaskExecutionId = taskExecution.Id,
                    ParametersJson = task.ParametersJson,
                    AttemptNumber = attempt,
                };

                var result = await handler.ExecuteAsync(context, timeoutCts.Token);

                taskExecution.FinishedAt = DateTime.UtcNow;

                if (result.Success)
                {
                    taskExecution.Status = TaskExecutionStatus.Succeeded;
                    taskExecution.OutputJson = NormalizeToJsonOrNull(result.OutputJson);
                }
                else
                {
                    taskExecution.Status = TaskExecutionStatus.Failed;
                    taskExecution.ErrorMessage = result.ErrorMessage;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                taskExecution.Status = TaskExecutionStatus.TimedOut;
                taskExecution.FinishedAt = DateTime.UtcNow;
                taskExecution.ErrorMessage = $"task timed out after {task.TimeoutSeconds}s (attempt {attempt}/{maxAttempts})";
            }
            catch (Exception ex)
            {
                taskExecution.Status = TaskExecutionStatus.Failed;
                taskExecution.FinishedAt = DateTime.UtcNow;
                taskExecution.ErrorMessage = ex.Message;
            }

            try
            {
                await executionRepository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // 保存执行结果本身失败（如插件返回的 OutputJson 不合法）不应该让整个 Worker
                // 消息处理挂起：把这次尝试记为失败，附带保存失败的原因，继续/结束重试循环。
                logger.LogError(ex, "Failed to save TaskExecution {TaskExecutionId} result", taskExecution.Id);
                taskExecution.Status = TaskExecutionStatus.Failed;
                taskExecution.ErrorMessage = $"failed to persist task result: {ex.Message}";
                taskExecution.OutputJson = null;
                await executionRepository.SaveChangesAsync(cancellationToken);
            }

            if (taskExecution.Status == TaskExecutionStatus.Succeeded)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// output_json 列是数据库原生 JSON 类型，写入非法 JSON 会导致 SaveChanges 失败。
    /// 插件契约约定 OutputJson 应为 JSON，但不能信任第三方插件一定遵守，这里做防御性校验：
    /// 不是合法 JSON 就丢弃原文本，只记录一个错误说明，不让格式问题掩盖了任务本身执行成功的事实。
    /// </summary>
    internal static string? NormalizeToJsonOrNull(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            return null;
        }

        try
        {
            using var _ = JsonDocument.Parse(outputJson);
            return outputJson;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { rawOutput = outputJson, warning = "handler returned non-JSON output" });
        }
    }
}
