using LabWeb.Data;
using LabWeb.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Hubs;

public class PointsBroadcaster(AppDbContext db, IHubContext<PointsHub> hub) : IDataChangeHandler
{
    public async Task HandleAsync(DataChange change, CancellationToken ct = default)
    {
        if (change.PointIds.Count == 0) return;

        var points = await db.Points.AsNoTracking()
            .Where(p => change.PointIds.Contains(p.Id))
            .ToMapPoints()
            .ToListAsync(ct);

        foreach (var point in points)
            await hub.Clients.All.SendAsync("pointChanged", point, ct);
        foreach (var id in change.PointIds.Except(points.Select(p => p.Id)))
            await hub.Clients.All.SendAsync("pointRemoved", id, ct);
    }
}
