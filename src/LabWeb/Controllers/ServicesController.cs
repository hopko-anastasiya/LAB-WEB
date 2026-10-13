using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class ServicesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Services.AsNoTracking().Include(s => s.Points).OrderBy(s => s.Name).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var service = await db.Services.AsNoTracking()
            .Include(s => s.Points)
            .FirstOrDefaultAsync(s => s.Id == id);
        return service is null ? NotFound() : View(service);
    }

    [Authorize(Policy = Policies.SystemAdmin)]
    public IActionResult Create() => View(new Service());

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> Create([Bind("Name,Description,Icon")] Service service)
    {
        await EnsureUniqueName(service);
        if (!ModelState.IsValid) return View(service);
        db.Add(service);
        await db.SaveChangesAsync();
        TempData["Success"] = "Послугу додано.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await db.Services.FindAsync(id);
        return service is null ? NotFound() : View(service);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Icon")] Service service)
    {
        if (id != service.Id) return NotFound();
        await EnsureUniqueName(service);
        if (!ModelState.IsValid) return View(service);

        var existing = await db.Services.FindAsync(id);
        if (existing is null) return NotFound();

        db.Entry(existing).CurrentValues.SetValues(service);
        await db.SaveChangesAsync();
        TempData["Success"] = "Зміни збережено.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await db.Services.AsNoTracking()
            .Include(s => s.Points)
            .FirstOrDefaultAsync(s => s.Id == id);
        return service is null ? NotFound() : View(service);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.SystemAdmin)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var service = await db.Services.FindAsync(id);
        if (service is null) return NotFound();
        db.Services.Remove(service);
        await db.SaveChangesAsync();
        TempData["Success"] = "Послугу видалено.";
        return RedirectToAction(nameof(Index));
    }

    private async Task EnsureUniqueName(Service service)
    {
        var name = service.Name?.Trim() ?? string.Empty;
        service.Name = name;
        if (await db.Services.AnyAsync(s => s.Id != service.Id && s.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(Service.Name), "Послуга з такою назвою вже існує.");
    }
}
