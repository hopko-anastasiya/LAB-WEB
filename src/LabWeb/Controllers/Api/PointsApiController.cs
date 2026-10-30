using LabWeb.Api;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/points")]
public class PointsApiController(AppDbContext db, IFileStorage files) : ApiControllerBase
{
    private const string PhotoFolder = "points";

    [HttpGet]
    public async Task<ActionResult<PagedResult<PointDto>>> List(
        [FromQuery] PageQuery page,
        [FromQuery] string? q,
        [FromQuery] string? settlement,
        [FromQuery] int? serviceId,
        [FromQuery] PointStatus? status,
        CancellationToken ct)
    {
        var query = WithIncludes();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term) || EF.Functions.ILike(p.Address, term));
        }
        if (!string.IsNullOrWhiteSpace(settlement))
            query = query.Where(p => EF.Functions.ILike(p.Settlement, settlement.Trim()));
        if (serviceId is not null)
            query = query.Where(p => p.Services.Any(s => s.Id == serviceId));
        if (status is not null)
            query = query.Where(p => p.Status == status);

        return await query.OrderBy(p => p.Id).ToPagedResultAsync(page, Request, ToDto, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PointDto>> Get(int id, CancellationToken ct)
    {
        var point = await WithIncludes().FirstOrDefaultAsync(p => p.Id == id, ct);
        return point is null ? NotFoundProblem($"Пункт {id} не знайдено") : ToDto(point);
    }

    [HttpPost]
    public async Task<ActionResult<PointDto>> Create(PointInput input, CancellationToken ct)
    {
        if (!await db.Operators.AnyAsync(o => o.Id == input.OperatorId, ct))
            return FieldError(nameof(input.OperatorId), $"Оператора {input.OperatorId} не існує");

        var point = new Point();
        Apply(input, point);
        point.Services = await LoadServices(input.ServiceIds, ct);
        db.Points.Add(point);
        await db.SaveChangesAsync(ct);

        var created = await WithIncludes().FirstAsync(p => p.Id == point.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = point.Id }, ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PointDto>> Update(int id, PointInput input, CancellationToken ct)
    {
        var point = await db.Points.Include(p => p.Services).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (point is null) return NotFoundProblem($"Пункт {id} не знайдено");
        if (!await db.Operators.AnyAsync(o => o.Id == input.OperatorId, ct))
            return FieldError(nameof(input.OperatorId), $"Оператора {input.OperatorId} не існує");

        Apply(input, point);
        point.UpdatedAt = DateTime.UtcNow;
        point.Services.Clear();
        foreach (var s in await LoadServices(input.ServiceIds, ct))
            point.Services.Add(s);

        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var point = await db.Points.Include(p => p.Reports).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (point is null) return NotFoundProblem($"Пункт {id} не знайдено");

        var photos = point.Reports.Select(r => r.PhotoPath).Append(point.PhotoPath).ToList();
        db.Points.Remove(point);
        await db.SaveChangesAsync(ct);
        foreach (var path in photos)
            await files.DeleteAsync(path, ct);

        return NoContent();
    }

    [HttpPost("{id:int}/photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PointDto>> UploadPhoto(int id, IFormFile? file, CancellationToken ct)
    {
        var point = await db.Points.FindAsync([id], ct);
        if (point is null) return NotFoundProblem($"Пункт {id} не знайдено");

        if (file is null) return FieldError("file", "Додайте файл зображення у полі file");
        var error = files.Validate(file);
        if (error is not null) return FieldError("file", error);

        await files.DeleteAsync(point.PhotoPath, ct);
        point.PhotoPath = await files.SaveAsync(file, PhotoFolder, ct);
        point.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}/photo")]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken ct)
    {
        var point = await db.Points.FindAsync([id], ct);
        if (point is null) return NotFoundProblem($"Пункт {id} не знайдено");

        await files.DeleteAsync(point.PhotoPath, ct);
        point.PhotoPath = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<Point> WithIncludes() =>
        db.Points.AsNoTracking().Include(p => p.Operator).Include(p => p.Services).AsSplitQuery();

    private Task<List<Service>> LoadServices(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return db.Services.Where(s => list.Contains(s.Id)).ToListAsync(ct);
    }

    private static void Apply(PointInput input, Point point)
    {
        point.Name = input.Name.Trim();
        point.Address = input.Address.Trim();
        point.Settlement = input.Settlement.Trim();
        point.Latitude = input.Latitude;
        point.Longitude = input.Longitude;
        point.Description = input.Description;
        point.Schedule = input.Schedule;
        point.Capacity = input.Capacity;
        point.Status = input.Status;
        point.OperatorId = input.OperatorId;
    }

    private PointDto ToDto(Point p) => new(
        p.Id, p.Name, p.Address, p.Settlement, p.Latitude, p.Longitude,
        p.Description, p.Schedule, p.Capacity, p.Status, AbsoluteUrl(p.PhotoPath),
        p.Operator is null ? null : new OperatorRef(p.Operator.Id, p.Operator.Name, p.Operator.Phone),
        p.Services.OrderBy(s => s.Name).Select(s => new ServiceRef(s.Id, s.Name, s.Icon)).ToList(),
        p.CreatedAt, p.UpdatedAt);
}
