namespace BackendJobService.Application.Interfaces;

/// <summary>
/// Scheduler 把待执行的 JobExecution 投递到消息队列，Worker 消费后拉取具体
/// Task 列表执行。消息体只携带 JobExecutionId，避免消息与数据库状态不一致。
/// </summary>
public interface ITaskExecutionPublisher
{
    Task PublishAsync(long jobExecutionId, CancellationToken cancellationToken);
}
