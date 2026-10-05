using LabWeb.Api;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Models;
using LabWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/map")]
public class MapApiController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("points")]
    public async Task<ActionResult<IReadOnlyList<MapPointDto>>> Points(
        [FromQuery] int[]? serviceId,
        [FromQuery] PointStatus? status,
        CancellationToken ct)
    {
        var query = db.Points.AsNoTracking();
        foreach (var id in serviceId ?? [])
            query = query.Where(p => p.Services.Any(s => s.Id == id));
        if (status is not null)
            query = query.Where(p => p.Status == status);

        return await query.OrderBy(p => p.Name).ToMapPoints().ToListAsync(ct);
    }
}
