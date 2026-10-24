using System.Text.Json;
using System.Threading.Channels;
using LabWeb.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LabWeb.Messaging;

public record PointStatusChanged(Guid EventId, int PointId, string PointName, PointStatus OldStatus, PointStatus NewStatus, DateTime ChangedAt);

public interface IStatusEventQueue
{
    Task PublishAsync(PointStatusChanged message, CancellationToken ct = default);
    Task ConsumeAsync(Func<PointStatusChanged, CancellationToken, Task> handler, CancellationToken ct);
}

public class InMemoryStatusEventQueue : IStatusEventQueue
{
    private readonly Channel<PointStatusChanged> _channel = Channel.CreateUnbounded<PointStatusChanged>();

    public Task PublishAsync(PointStatusChanged message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct).AsTask();

    public async Task ConsumeAsync(Func<PointStatusChanged, CancellationToken, Task> handler, CancellationToken ct)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(ct))
            await handler(message, ct);
    }
}

public class RabbitMqOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public sealed class RabbitMqStatusEventQueue(RabbitMqOptions options, ILogger<RabbitMqStatusEventQueue> logger)
    : IStatusEventQueue, IAsyncDisposable
{
    private const string Queue = "point-status-changed";
    private const string DeadQueue = "point-status-changed.dead";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publisher;

    public async Task PublishAsync(PointStatusChanged message, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_publisher is not { IsOpen: true })
            {
                var connection = await ConnectAsync(ct);
                _publisher = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await DeclareAsync(_publisher, ct);
            }

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.EventId.ToString()
            };
            await _publisher.BasicPublishAsync(string.Empty, Queue, true, properties, JsonSerializer.SerializeToUtf8Bytes(message), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ConsumeAsync(Func<PointStatusChanged, CancellationToken, Task> handler, CancellationToken ct)
    {
        IConnection connection;
        await _gate.WaitAsync(ct);
        try
        {
            connection = await ConnectAsync(ct);
        }
        finally
        {
            _gate.Release();
        }

        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await DeclareAsync(channel, ct);
        await channel.BasicQosAsync(0, 10, false, ct);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<PointStatusChanged>(delivery.Body.Span)
                              ?? throw new JsonException("Порожнє повідомлення");
                await handler(message, ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Не вдалося обробити повідомлення {MessageId}", delivery.BasicProperties.MessageId);
                await channel.BasicNackAsync(delivery.DeliveryTag, false, !delivery.Redelivered && ex is not JsonException, ct);
            }
        };

        await channel.BasicConsumeAsync(Queue, false, consumer, ct);
        await closed.Task.WaitAsync(ct);
    }

    private async Task<IConnection> ConnectAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;
        var factory = new ConnectionFactory
        {
            HostName = options.Host!,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            AutomaticRecoveryEnabled = true
        };
        return _connection = await factory.CreateConnectionAsync("labweb", ct);
    }

    private static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(DeadQueue, true, false, false, cancellationToken: ct);
        await channel.QueueDeclareAsync(Queue, true, false, false, new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = DeadQueue
        }, cancellationToken: ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisher is not null) await _publisher.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
