using LabWeb.Api;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/search")]
public class SearchApiController(AppDbContext db, PointSearchIndex index) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SearchHitDto>>> Search([FromQuery] SearchQuery query, CancellationToken ct)
    {
        var (matches, total) = index.Search(query.Q, query.Status, query.Skip, query.Limit);
        var ids = matches.Select(m => m.PointId).ToList();
        var points = await db.Points.AsNoTracking()
            .Include(p => p.Services)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var items = matches
            .Where(m => points.ContainsKey(m.PointId))
            .Select(m =>
            {
                var p = points[m.PointId];
                return new SearchHitDto(p.Id, p.Name, p.Settlement, p.Address, p.Status,
                    p.Services.Select(s => s.Name).OrderBy(n => n).ToList(), MathF.Round(m.Score, 3));
            })
            .ToList();

        return PagingExtensions.ToPagedResult(items, query, total, Request);
    }
}
