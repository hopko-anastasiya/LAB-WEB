using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement.Mvc;

namespace LabWeb.Controllers;

[Authorize(Policy = Policies.Member)]
[FeatureGate(Features.Analytics)]
public class AnalyticsController(AppDbContext db, CurrentUser currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var communityId = currentUser.CommunityId;
        var since = DateTime.UtcNow.Date.AddDays(-6);

        var points = await db.Points.AsNoTracking()
            .Where(p => p.Operator!.CommunityId == communityId)
            .Select(p => new
            {
                p.Id, p.Name, p.Status, p.Capacity,
                Last = p.Reports.OrderByDescending(r => r.CreatedAt).Select(r => new { r.Occupancy, r.HasElectricity }).FirstOrDefault()
            })
            .ToListAsync();

        var reports = await db.StatusReports.AsNoTracking()
            .Where(r => r.Point!.Operator!.CommunityId == communityId && r.CreatedAt >= since)
            .Select(r => new { r.CreatedAt, r.HasElectricity })
            .ToListAsync();

        var vm = new AnalyticsViewModel
        {
            CommunityName = currentUser.Community!.Name,
            ByStatus = Enum.GetValues<PointStatus>().ToDictionary(s => s, s => points.Count(p => p.Status == s)),
            Days = Enumerable.Range(0, 7)
                .Select(i => since.AddDays(i))
                .Select(day => new AnalyticsDay(day,
                    reports.Count(r => r.CreatedAt.Date == day),
                    reports.Count(r => r.CreatedAt.Date == day && r.HasElectricity)))
                .ToList(),
            Load = points
                .Select(p => new PointLoad(p.Id, p.Name, p.Status, p.Last?.Occupancy, p.Capacity))
                .OrderByDescending(p => p.Percent ?? -1)
                .ToList(),
            WithoutElectricity = points.Count(p => p.Last is { HasElectricity: false })
        };
        return View(vm);
    }
}
