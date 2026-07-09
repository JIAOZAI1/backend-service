using System.Text;
using System.Text.Json;
using BackendJobService.Application.Interfaces;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BackendJobService.Infrastructure.Messaging;

public class RabbitMqTaskExecutionPublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : ITaskExecutionPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishAsync(long jobExecutionId, CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var message = JsonSerializer.Serialize(new TaskExecutionMessage(jobExecutionId));
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}

public record TaskExecutionMessage(long JobExecutionId);
