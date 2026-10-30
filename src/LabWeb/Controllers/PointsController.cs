using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Models.ViewModels;
using LabWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class PointsController(AppDbContext db, IFileStorage files) : Controller
{
    private const string PhotoFolder = "points";

    public async Task<IActionResult> Index(string? q, int? serviceId, PointStatus? status)
    {
        var query = db.Points.AsNoTracking()
            .Include(p => p.Operator)
            .Include(p => p.Services)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term)
                                  || EF.Functions.ILike(p.Address, term)
                                  || EF.Functions.ILike(p.Settlement, term));
        }
        if (serviceId is not null)
            query = query.Where(p => p.Services.Any(s => s.Id == serviceId));
        if (status is not null)
            query = query.Where(p => p.Status == status);

        ViewData["q"] = q;
        ViewData["serviceId"] = serviceId;
        ViewData["status"] = status;
        ViewBag.Services = new SelectList(await db.Services.OrderBy(s => s.Name).ToListAsync(), "Id", "Name", serviceId);

        return View(await query.OrderBy(p => p.Settlement).ThenBy(p => p.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var point = await db.Points.AsNoTracking()
            .Include(p => p.Operator)
            .Include(p => p.Services)
            .Include(p => p.Reports.OrderByDescending(r => r.CreatedAt).Take(10))
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
        return point is null ? NotFound() : View(point);
    }

    public async Task<IActionResult> Create()
    {
        var vm = new PointFormViewModel { Point = { Latitude = 50.4501, Longitude = 30.5234, Settlement = "Київ" } };
        await FillLookups(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PointFormViewModel vm)
    {
        ValidatePhoto(vm.Photo);
        await ValidateOperator(vm.Point.OperatorId);
        if (!ModelState.IsValid)
        {
            await FillLookups(vm);
            return View(vm);
        }

        var point = new Point();
        CopyEditableFields(vm.Point, point);
        point.Services = await db.Services.Where(s => vm.SelectedServiceIds.Contains(s.Id)).ToListAsync();
        if (vm.Photo is not null)
            point.PhotoPath = await files.SaveAsync(vm.Photo, PhotoFolder);

        db.Points.Add(point);
        await db.SaveChangesAsync();
        TempData["Success"] = "Пункт створено.";
        return RedirectToAction(nameof(Details), new { id = point.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var point = await db.Points.AsNoTracking().Include(p => p.Services).FirstOrDefaultAsync(p => p.Id == id);
        if (point is null) return NotFound();

        var vm = new PointFormViewModel
        {
            Point = point,
            SelectedServiceIds = point.Services.Select(s => s.Id).ToList()
        };
        await FillLookups(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PointFormViewModel vm)
    {
        if (id != vm.Point.Id) return NotFound();

        var point = await db.Points.Include(p => p.Services).FirstOrDefaultAsync(p => p.Id == id);
        if (point is null) return NotFound();

        ValidatePhoto(vm.Photo);
        await ValidateOperator(vm.Point.OperatorId);
        if (!ModelState.IsValid)
        {
            vm.Point.PhotoPath = point.PhotoPath;
            await FillLookups(vm);
            return View(vm);
        }

        CopyEditableFields(vm.Point, point);
        point.UpdatedAt = DateTime.UtcNow;

        point.Services.Clear();
        foreach (var s in await db.Services.Where(s => vm.SelectedServiceIds.Contains(s.Id)).ToListAsync())
            point.Services.Add(s);

        if (vm.Photo is not null || vm.RemovePhoto)
        {
            await files.DeleteAsync(point.PhotoPath);
            point.PhotoPath = vm.Photo is not null ? await files.SaveAsync(vm.Photo, PhotoFolder) : null;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Зміни збережено.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var point = await db.Points.AsNoTracking()
            .Include(p => p.Operator)
            .Include(p => p.Reports)
            .FirstOrDefaultAsync(p => p.Id == id);
        return point is null ? NotFound() : View(point);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var point = await db.Points.Include(p => p.Reports).FirstOrDefaultAsync(p => p.Id == id);
        if (point is null) return NotFound();

        var photos = point.Reports.Select(r => r.PhotoPath).Append(point.PhotoPath).ToList();
        db.Points.Remove(point);
        await db.SaveChangesAsync();
        foreach (var path in photos)
            await files.DeleteAsync(path);

        TempData["Success"] = "Пункт видалено.";
        return RedirectToAction(nameof(Index));
    }

    private static void CopyEditableFields(Point src, Point dst)
    {
        dst.Name = src.Name.Trim();
        dst.Address = src.Address.Trim();
        dst.Settlement = src.Settlement.Trim();
        dst.Latitude = src.Latitude;
        dst.Longitude = src.Longitude;
        dst.Description = src.Description;
        dst.Schedule = src.Schedule;
        dst.Capacity = src.Capacity;
        dst.Status = src.Status;
        dst.OperatorId = src.OperatorId;
    }

    private void ValidatePhoto(IFormFile? photo)
    {
        if (photo is null) return;
        var error = files.Validate(photo);
        if (error is not null)
            ModelState.AddModelError(nameof(PointFormViewModel.Photo), error);
    }

    private async Task ValidateOperator(int operatorId)
    {
        if (!await db.Operators.AnyAsync(o => o.Id == operatorId))
            ModelState.AddModelError("Point.OperatorId", "Оберіть оператора.");
    }

    private async Task FillLookups(PointFormViewModel vm)
    {
        vm.Operators = new SelectList(await db.Operators.Where(o => o.Id == vm.Point.OperatorId).ToListAsync(), "Id", "Name", vm.Point.OperatorId);
        vm.AllServices = await db.Services.OrderBy(s => s.Name).ToListAsync();
    }
}
