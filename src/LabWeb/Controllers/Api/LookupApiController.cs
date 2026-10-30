using LabWeb.Api;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/lookup")]
public class LookupApiController(AppDbContext db) : ApiControllerBase
{
    [HttpGet("settlements")]
    public async Task<ActionResult<IReadOnlyList<SettlementDto>>> Settlements([FromQuery] LookupQuery query, CancellationToken ct)
    {
        var start = query.Q.Trim();
        var settlements = db.Settlements.AsNoTracking();
        foreach (var word in query.Words())
            settlements = settlements.Where(s => EF.Functions.ILike(s.Name, word) || EF.Functions.ILike(s.Region, word));

        return await settlements
            .OrderByDescending(s => EF.Functions.ILike(s.Name, start))
            .ThenByDescending(s => EF.Functions.ILike(s.Name, start + "%"))
            .ThenByDescending(s => s.Population)
            .ThenBy(s => s.Name)
            .Take(query.Limit)
            .Select(s => new SettlementDto(s.Id, s.Name, s.Region, s.Latitude, s.Longitude, s.Population))
            .ToListAsync(ct);
    }

    [HttpGet("operators")]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> Operators([FromQuery] LookupQuery query, CancellationToken ct)
    {
        var operators = db.Operators.AsNoTracking();
        foreach (var word in query.Words())
            operators = operators.Where(o => EF.Functions.ILike(o.Name, word)
                                          || (o.Organization != null && EF.Functions.ILike(o.Organization, word))
                                          || (o.Telegram != null && EF.Functions.ILike(o.Telegram, word)));

        return await operators.OrderBy(o => o.Name).Take(query.Limit)
            .Select(o => new LookupItem(o.Id, o.Name, o.Organization))
            .ToListAsync(ct);
    }

    [HttpGet("points")]
    public async Task<ActionResult<IReadOnlyList<LookupItem>>> Points([FromQuery] LookupQuery query, CancellationToken ct)
    {
        var points = db.Points.AsNoTracking();
        foreach (var word in query.Words())
            points = points.Where(p => EF.Functions.ILike(p.Name, word)
                                    || EF.Functions.ILike(p.Address, word)
                                    || EF.Functions.ILike(p.Settlement, word));

        return await points.OrderBy(p => p.Name).Take(query.Limit)
            .Select(p => new LookupItem(p.Id, p.Name, p.Settlement + ", " + p.Address))
            .ToListAsync(ct);
    }
}
