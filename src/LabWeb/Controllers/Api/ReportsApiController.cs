using LabWeb.Api;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api")]
public class ReportsApiController(AppDbContext db, IFileStorage files, PointStatusService statuses) : ApiControllerBase
{
    private const string PhotoFolder = "reports";

    [HttpGet("reports")]
    public async Task<ActionResult<PagedResult<ReportDto>>> List(
        [FromQuery] PageQuery page,
        [FromQuery] int? pointId,
        [FromQuery] PointStatus? status,
        CancellationToken ct)
    {
        var query = db.StatusReports.AsNoTracking().Include(r => r.Point).AsQueryable();
        if (pointId is not null)
            query = query.Where(r => r.PointId == pointId);
        if (status is not null)
            query = query.Where(r => r.Status == status);

        return await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToPagedResultAsync(page, Request, ToDto, ct);
    }

    [HttpGet("points/{pointId:int}/reports")]
    public async Task<ActionResult<PagedResult<ReportDto>>> ListForPoint(int pointId, [FromQuery] PageQuery page, CancellationToken ct)
    {
        if (!await db.Points.AnyAsync(p => p.Id == pointId, ct))
            return NotFoundProblem($"Пункт {pointId} не знайдено");
        return await List(page, pointId, null, ct);
    }

    [HttpGet("reports/{id:int}")]
    public async Task<ActionResult<ReportDto>> Get(int id, CancellationToken ct)
    {
        var report = await db.StatusReports.AsNoTracking().Include(r => r.Point).FirstOrDefaultAsync(r => r.Id == id, ct);
        return report is null ? NotFoundProblem($"Звіт {id} не знайдено") : ToDto(report);
    }

    [HttpPost("reports")]
    public async Task<ActionResult<ReportDto>> Create(ReportInput input, CancellationToken ct)
    {
        var point = await db.Points.FindAsync([input.PointId], ct);
        if (point is null)
            return FieldError(nameof(input.PointId), $"Пункту {input.PointId} не існує");

        var report = new StatusReport { CreatedAt = DateTime.UtcNow };
        Apply(input, report);
        db.StatusReports.Add(report);
        statuses.ApplyNewReport(point, report);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(report);
        return CreatedAtAction(nameof(Get), new { id = report.Id }, dto);
    }

    [HttpPut("reports/{id:int}")]
    public async Task<ActionResult<ReportDto>> Update(int id, ReportInput input, CancellationToken ct)
    {
        var report = await db.StatusReports.FindAsync([id], ct);
        if (report is null) return NotFoundProblem($"Звіт {id} не знайдено");
        if (!await db.Points.AnyAsync(p => p.Id == input.PointId, ct))
            return FieldError(nameof(input.PointId), $"Пункту {input.PointId} не існує");

        var oldPointId = report.PointId;
        Apply(input, report);
        await db.SaveChangesAsync(ct);

        await statuses.RecalculateAsync(report.PointId, ct);
        if (oldPointId != report.PointId)
            await statuses.RecalculateAsync(oldPointId, ct);

        return await Get(id, ct);
    }

    [HttpDelete("reports/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var report = await db.StatusReports.FindAsync([id], ct);
        if (report is null) return NotFoundProblem($"Звіт {id} не знайдено");

        db.StatusReports.Remove(report);
        await db.SaveChangesAsync(ct);
        await files.DeleteAsync(report.PhotoPath, ct);
        await statuses.RecalculateAsync(report.PointId, ct);
        return NoContent();
    }

    [HttpPost("reports/{id:int}/photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReportDto>> UploadPhoto(int id, IFormFile? file, CancellationToken ct)
    {
        var report = await db.StatusReports.FindAsync([id], ct);
        if (report is null) return NotFoundProblem($"Звіт {id} не знайдено");

        if (file is null) return FieldError("file", "Додайте файл зображення у полі file");
        var error = files.Validate(file);
        if (error is not null) return FieldError("file", error);

        await files.DeleteAsync(report.PhotoPath, ct);
        report.PhotoPath = await files.SaveAsync(file, PhotoFolder, ct);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("reports/{id:int}/photo")]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken ct)
    {
        var report = await db.StatusReports.FindAsync([id], ct);
        if (report is null) return NotFoundProblem($"Звіт {id} не знайдено");

        await files.DeleteAsync(report.PhotoPath, ct);
        report.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(ReportInput input, StatusReport report)
    {
        report.PointId = input.PointId;
        report.Status = input.Status;
        report.HasElectricity = input.HasElectricity;
        report.HasInternet = input.HasInternet;
        report.HasHeating = input.HasHeating;
        report.Occupancy = input.Occupancy;
        report.Comment = input.Comment;
        report.AuthorName = input.AuthorName;
    }

    private ReportDto ToDto(StatusReport r) => new(
        r.Id, r.PointId, r.Point?.Name, r.Status, r.HasElectricity, r.HasInternet, r.HasHeating,
        r.Occupancy, r.Comment, AbsoluteUrl(r.PhotoPath), r.AuthorName, r.CreatedAt);
}
