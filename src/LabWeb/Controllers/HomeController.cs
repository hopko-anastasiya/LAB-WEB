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
            Points = await db.Points.AsNoTracking()
                .Include(p => p.Services)
                .OrderBy(p => p.Status).ThenBy(p => p.Name)
                .Take(6).ToListAsync(),
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
