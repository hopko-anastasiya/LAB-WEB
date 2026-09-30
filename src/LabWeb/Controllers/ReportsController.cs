using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Models.ViewModels;
using LabWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class ReportsController(AppDbContext db, IFileStorage files, PointStatusService statuses) : Controller
{
    private const string PhotoFolder = "reports";

    public async Task<IActionResult> Index(int? pointId)
    {
        var query = db.StatusReports.AsNoTracking().Include(r => r.Point).AsQueryable();
        if (pointId is not null)
            query = query.Where(r => r.PointId == pointId);

        ViewData["pointId"] = pointId;
        ViewBag.Points = new SelectList(await db.Points.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", pointId);
        return View(await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var report = await db.StatusReports.AsNoTracking()
            .Include(r => r.Point)
            .FirstOrDefaultAsync(r => r.Id == id);
        return report is null ? NotFound() : View(report);
    }

    public async Task<IActionResult> Create(int? pointId)
    {
        var vm = new ReportFormViewModel();
        if (pointId is not null)
        {
            var point = await db.Points.FindAsync(pointId);
            if (point is not null)
            {
                vm.Report.PointId = point.Id;
                vm.Report.Status = point.Status;
            }
        }
        await FillLookups(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReportFormViewModel vm)
    {
        ValidatePhoto(vm.Photo);
        var point = await db.Points.FindAsync(vm.Report.PointId);
        if (point is null)
            ModelState.AddModelError("Report.PointId", "Оберіть пункт.");

        if (!ModelState.IsValid)
        {
            await FillLookups(vm);
            return View(vm);
        }

        var report = new StatusReport();
        CopyEditableFields(vm.Report, report);
        report.CreatedAt = DateTime.UtcNow;
        if (vm.Photo is not null)
            report.PhotoPath = await files.SaveAsync(vm.Photo, PhotoFolder);

        db.StatusReports.Add(report);
        statuses.ApplyNewReport(point!, report);

        await db.SaveChangesAsync();
        TempData["Success"] = "Звіт додано, статус пункту оновлено.";
        return RedirectToAction("Details", "Points", new { id = report.PointId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var report = await db.StatusReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return NotFound();
        var vm = new ReportFormViewModel { Report = report };
        await FillLookups(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ReportFormViewModel vm)
    {
        if (id != vm.Report.Id) return NotFound();
        var report = await db.StatusReports.FindAsync(id);
        if (report is null) return NotFound();

        ValidatePhoto(vm.Photo);
        if (!await db.Points.AnyAsync(p => p.Id == vm.Report.PointId))
            ModelState.AddModelError("Report.PointId", "Оберіть пункт.");

        if (!ModelState.IsValid)
        {
            vm.Report.PhotoPath = report.PhotoPath;
            await FillLookups(vm);
            return View(vm);
        }

        var oldPointId = report.PointId;
        CopyEditableFields(vm.Report, report);

        if (vm.Photo is not null || vm.RemovePhoto)
        {
            await files.DeleteAsync(report.PhotoPath);
            report.PhotoPath = vm.Photo is not null ? await files.SaveAsync(vm.Photo, PhotoFolder) : null;
        }

        await db.SaveChangesAsync();
        await statuses.RecalculateAsync(report.PointId);
        if (oldPointId != report.PointId)
            await statuses.RecalculateAsync(oldPointId);

        TempData["Success"] = "Зміни збережено.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var report = await db.StatusReports.AsNoTracking()
            .Include(r => r.Point)
            .FirstOrDefaultAsync(r => r.Id == id);
        return report is null ? NotFound() : View(report);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var report = await db.StatusReports.FindAsync(id);
        if (report is null) return NotFound();

        var pointId = report.PointId;
        var photo = report.PhotoPath;
        db.StatusReports.Remove(report);
        await db.SaveChangesAsync();
        await files.DeleteAsync(photo);
        await statuses.RecalculateAsync(pointId);

        TempData["Success"] = "Звіт видалено.";
        return RedirectToAction("Details", "Points", new { id = pointId });
    }


    private static void CopyEditableFields(StatusReport src, StatusReport dst)
    {
        dst.PointId = src.PointId;
        dst.Status = src.Status;
        dst.HasElectricity = src.HasElectricity;
        dst.HasInternet = src.HasInternet;
        dst.HasHeating = src.HasHeating;
        dst.Occupancy = src.Occupancy;
        dst.Comment = src.Comment;
        dst.AuthorName = src.AuthorName;
    }

    private void ValidatePhoto(IFormFile? photo)
    {
        if (photo is null) return;
        var error = files.Validate(photo);
        if (error is not null)
            ModelState.AddModelError(nameof(ReportFormViewModel.Photo), error);
    }

    private async Task FillLookups(ReportFormViewModel vm) =>
        vm.Points = new SelectList(await db.Points.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", vm.Report.PointId);
}
