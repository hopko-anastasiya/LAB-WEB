using LabWeb.Data;
using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Services;

public class PointStatusService(AppDbContext db)
{
    public void ApplyNewReport(Point point, StatusReport report)
    {
        point.Status = report.Status;
        point.UpdatedAt = report.CreatedAt;
    }

    public async Task RecalculateAsync(int pointId, CancellationToken ct = default)
    {
        var point = await db.Points.FindAsync([pointId], ct);
        if (point is null) return;

        var latest = await db.StatusReports
            .Where(r => r.PointId == pointId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return;

        point.Status = latest.Status;
        point.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
