using System.Runtime.CompilerServices;
using LabWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LabWeb.Services;

public record PointStatusChange(Func<int> PointId, string PointName, PointStatus OldStatus, PointStatus NewStatus);

public record DataChange(IReadOnlyList<int> PointIds, bool OperatorsOrServicesChanged, IReadOnlyList<PointStatusChange> StatusChanges);

public interface IDataChangeHandler
{
    Task HandleAsync(DataChange change, CancellationToken ct = default);
}

public class DataChangesInterceptor(IServiceScopeFactory scopes, ILogger<DataChangesInterceptor> logger)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    private class Pending
    {
        public List<Func<int>> PointIds { get; } = [];
        public bool OperatorsOrServices { get; set; }
        public List<PointStatusChange> StatusChanges { get; } = [];
    }

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
            && (pending.PointIds.Count > 0 || pending.OperatorsOrServices))
        {
            var change = new DataChange(pending.PointIds.Select(id => id()).Distinct().ToList(), pending.OperatorsOrServices, pending.StatusChanges);
            await DispatchAsync(change);
        }
        return await base.SavedChangesAsync(eventData, result, ct);
    }

    private static Pending Collect(DbContext context)
    {
        var pending = new Pending();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            switch (entry.Entity)
            {
                case Point point:
                    pending.PointIds.Add(() => point.Id);
                    if (entry.State == EntityState.Modified
                        && entry.Property(nameof(Point.Status)).OriginalValue is PointStatus oldStatus
                        && oldStatus != point.Status)
                        pending.StatusChanges.Add(new PointStatusChange(() => point.Id, point.Name, oldStatus, point.Status));
                    break;
                case StatusReport report:
                    pending.PointIds.Add(() => report.Point?.Id ?? report.PointId);
                    if (entry.State == EntityState.Modified
                        && entry.Property(nameof(StatusReport.PointId)).OriginalValue is int oldPointId)
                        pending.PointIds.Add(() => oldPointId);
                    break;
                case Operator or Service:
                    pending.OperatorsOrServices = true;
                    break;
            }
        }
        return pending;
    }

    private async Task DispatchAsync(DataChange change)
    {
        using var scope = scopes.CreateScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IDataChangeHandler>())
        {
            try
            {
                await handler.HandleAsync(change);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Handler} не обробив зміни пунктів {Ids}", handler.GetType().Name, change.PointIds);
            }
        }
    }
}
