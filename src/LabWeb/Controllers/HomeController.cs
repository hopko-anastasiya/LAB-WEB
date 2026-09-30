using System.Diagnostics;
using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var vm = new HomeViewModel
        {
            PointsTotal = await db.Points.CountAsync(),
            PointsWorking = await db.Points.CountAsync(p => p.Status == PointStatus.Working),
            OperatorsTotal = await db.Operators.CountAsync(),
            ReportsToday = await db.StatusReports.CountAsync(r => r.CreatedAt >= since),
            Skyline = (await db.Points.AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id, p.Name, p.Settlement, p.Status,
                    Electricity = p.Reports.OrderByDescending(r => r.CreatedAt)
                        .Select(r => (bool?)r.HasElectricity).FirstOrDefault()
                })
                .ToListAsync())
                .Select(p => new SkylinePoint(p.Id, p.Name, p.Settlement, p.Status,
                    p.Status != PointStatus.Closed && (p.Electricity ?? p.Status == PointStatus.Working)))
                .ToList(),
            Points = await db.Points.AsNoTracking()
                .Include(p => p.Services)
                .OrderBy(p => p.Status == PointStatus.Working ? 0 : p.Status == PointStatus.Limited ? 1 : 2)
                .ThenBy(p => p.Name)
                .Take(8).ToListAsync(),
            LatestReports = await db.StatusReports.AsNoTracking()
                .Include(r => r.Point)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5).ToListAsync()
        };
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
