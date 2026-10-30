using LabWeb.Api;
using LabWeb.Auth;
using Microsoft.AspNetCore.Authorization;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/services")]
public class ServicesApiController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ServiceDto>>> List([FromQuery] PageQuery page, [FromQuery] string? q, CancellationToken ct)
    {
        var query = db.Services.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{q.Trim()}%"));

        var projected = query.OrderBy(s => s.Id)
            .Select(s => new ServiceDto(s.Id, s.Name, s.Description, s.Icon, s.Points.Count));
        return await projected.ToPagedResultAsync(page, Request, x => x, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceDto>> Get(int id, CancellationToken ct)
    {
        var dto = await db.Services.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new ServiceDto(s.Id, s.Name, s.Description, s.Icon, s.Points.Count))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFoundProblem($"Послугу {id} не знайдено") : dto;
    }

    [HttpPost, Authorize(Policy = Policies.ApiSystemAdmin)]
    public async Task<ActionResult<ServiceDto>> Create(ServiceInput input, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (await NameTaken(name, null, ct))
            return ConflictProblem($"Послуга «{name}» вже існує");

        var service = new Service { Name = name, Description = input.Description, Icon = input.Icon };
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);

        var dto = new ServiceDto(service.Id, service.Name, service.Description, service.Icon, 0);
        return CreatedAtAction(nameof(Get), new { id = service.Id }, dto);
    }

    [HttpPut("{id:int}"), Authorize(Policy = Policies.ApiSystemAdmin)]
    public async Task<ActionResult<ServiceDto>> Update(int id, ServiceInput input, CancellationToken ct)
    {
        var service = await db.Services.FindAsync([id], ct);
        if (service is null) return NotFoundProblem($"Послугу {id} не знайдено");

        var name = input.Name.Trim();
        if (await NameTaken(name, id, ct))
            return ConflictProblem($"Послуга «{name}» вже існує");

        service.Name = name;
        service.Description = input.Description;
        service.Icon = input.Icon;
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}"), Authorize(Policy = Policies.ApiSystemAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var service = await db.Services.FindAsync([id], ct);
        if (service is null) return NotFoundProblem($"Послугу {id} не знайдено");

        db.Services.Remove(service);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> NameTaken(string name, int? exceptId, CancellationToken ct) =>
        db.Services.AnyAsync(s => s.Id != exceptId && s.Name.ToLower() == name.ToLower(), ct);
}
