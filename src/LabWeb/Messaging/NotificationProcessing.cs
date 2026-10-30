using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Messaging;

public class StatusChangePublisher(IStatusEventQueue queue) : IDataChangeHandler
{
    public async Task HandleAsync(DataChange change, CancellationToken ct = default)
    {
        foreach (var c in change.StatusChanges)
            await queue.PublishAsync(new PointStatusChanged(Guid.NewGuid(), c.PointId(), c.PointName, c.OldStatus, c.NewStatus, DateTime.UtcNow), ct);
    }
}

public class NotificationWriter(AppDbContext db)
{
    public async Task WriteAsync(PointStatusChanged message, CancellationToken ct = default)
    {
        var users = await db.PointSubscriptions
            .Where(s => s.PointId == message.PointId)
            .Where(s => !db.Notifications.Any(n => n.EventId == message.EventId && n.UserId == s.UserId))
            .Select(s => s.UserId)
            .ToListAsync(ct);

        db.Notifications.AddRange(users.Select(userId => new Notification
        {
            UserId = userId,
            EventId = message.EventId,
            PointId = message.PointId,
            PointName = message.PointName,
            OldStatus = message.OldStatus,
            NewStatus = message.NewStatus,
            ChangedAt = message.ChangedAt
        }));
        await db.SaveChangesAsync(ct);
    }
}

public class NotificationConsumerService(
    IStatusEventQueue queue,
    IServiceScopeFactory scopes,
    ILogger<NotificationConsumerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await queue.ConsumeAsync(async (message, ct) =>
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationWriter>().WriteAsync(message, ct);
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Черга сповіщень недоступна, повтор через 5 с");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
