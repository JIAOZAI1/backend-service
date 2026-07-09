namespace BackendJobService.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string Host { get; init; }
    public int Port { get; init; } = 5672;
    public required string VirtualHost { get; init; } = "/";
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string QueueName { get; init; } = "backend-job-service.task-executions";
}
