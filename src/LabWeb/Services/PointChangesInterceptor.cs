using System.Runtime.CompilerServices;
using LabWeb.Data;
using LabWeb.Hubs;
using LabWeb.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LabWeb.Services;

public class PointChangesInterceptor(IServiceScopeFactory scopes, ILogger<PointChangesInterceptor> logger)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<Func<int>>> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null)
            _pending.AddOrUpdate(eventData.Context, Collect(eventData.Context));
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (eventData.Context is not null
            && _pending.TryGetValue(eventData.Context, out var pending)
            && _pending.Remove(eventData.Context)
            && pending.Count > 0)
            await BroadcastAsync(pending.Select(id => id()).Distinct().ToList());
        return await base.SavedChangesAsync(eventData, result, ct);
    }

    private static List<Func<int>> Collect(DbContext context)
    {
        var ids = new List<Func<int>>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            switch (entry.Entity)
            {
                case Point point:
                    ids.Add(() => point.Id);
                    break;
                case StatusReport report:
                    ids.Add(() => report.Point?.Id ?? report.PointId);
                    if (entry.State == EntityState.Modified)
                    {
                        var oldPointId = entry.Property(nameof(StatusReport.PointId)).OriginalValue as int?;
                        if (oldPointId is int old)
                            ids.Add(() => old);
                    }
                    break;
            }
        }
        return ids;
    }

    private async Task BroadcastAsync(IReadOnlyList<int> pointIds)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<PointsHub>>();

            var points = await db.Points.AsNoTracking()
                .Where(p => pointIds.Contains(p.Id))
                .ToMapPoints()
                .ToListAsync();

            foreach (var point in points)
                await hub.Clients.All.SendAsync("pointChanged", point);
            foreach (var id in pointIds.Except(points.Select(p => p.Id)))
                await hub.Clients.All.SendAsync("pointRemoved", id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не вдалося розіслати зміни пунктів {Ids}", pointIds);
        }
    }
}
