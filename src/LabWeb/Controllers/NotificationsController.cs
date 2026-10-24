using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

[Authorize]
public class NotificationsController(AppDbContext db, CurrentUser currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = currentUser.User!.Id;
        ViewBag.Subscriptions = await db.PointSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Include(s => s.Point)
            .OrderBy(s => s.Point!.Name)
            .ToListAsync();
        return View(await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.ChangedAt)
            .Take(100)
            .ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead()
    {
        var userId = currentUser.User!.Id;
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(int pointId)
    {
        var userId = currentUser.User!.Id;
        if (!await db.Points.AnyAsync(p => p.Id == pointId)) return NotFound();
        if (!await db.PointSubscriptions.AnyAsync(s => s.UserId == userId && s.PointId == pointId))
        {
            db.PointSubscriptions.Add(new PointSubscription { UserId = userId, PointId = pointId });
            await db.SaveChangesAsync();
        }
        TempData["Success"] = "Ви стежите за пунктом. Сповістимо, коли зміниться його статус.";
        return RedirectToAction("Details", "Points", new { id = pointId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe(int pointId)
    {
        var userId = currentUser.User!.Id;
        await db.PointSubscriptions.Where(s => s.UserId == userId && s.PointId == pointId).ExecuteDeleteAsync();
        TempData["Success"] = "Ви більше не стежите за цим пунктом.";
        return RedirectToAction("Details", "Points", new { id = pointId });
    }
}
