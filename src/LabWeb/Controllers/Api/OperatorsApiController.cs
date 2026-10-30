using LabWeb.Api;
using LabWeb.Auth;
using Microsoft.AspNetCore.Authorization;
using LabWeb.Api.Dtos;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers.Api;

[Route("api/operators")]
public class OperatorsApiController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OperatorDto>>> List([FromQuery] PageQuery page, [FromQuery] string? q, CancellationToken ct)
    {
        var query = db.Operators.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.Name, term)
                                  || (o.Organization != null && EF.Functions.ILike(o.Organization, term)));
        }

        var projected = query.OrderBy(o => o.Id)
            .Select(o => new OperatorDto(o.Id, o.Name, o.Organization, o.Phone, o.Email, o.Telegram, o.CommunityId, o.Points.Count));
        return await projected.ToPagedResultAsync(page, Request, x => x, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OperatorDto>> Get(int id, CancellationToken ct)
    {
        var dto = await db.Operators.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OperatorDto(o.Id, o.Name, o.Organization, o.Phone, o.Email, o.Telegram, o.CommunityId, o.Points.Count))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFoundProblem($"Оператора {id} не знайдено") : dto;
    }

    [HttpPost, Authorize(Policy = Policies.ApiWriter)]
    public async Task<ActionResult<OperatorDto>> Create(OperatorInput input, CancellationToken ct)
    {
        var op = new Operator { CommunityId = CurrentUser.CommunityId!.Value };
        Apply(input, op);
        db.Operators.Add(op);
        await db.SaveChangesAsync(ct);

        var dto = new OperatorDto(op.Id, op.Name, op.Organization, op.Phone, op.Email, op.Telegram, op.CommunityId, 0);
        return CreatedAtAction(nameof(Get), new { id = op.Id }, dto);
    }

    [HttpPut("{id:int}"), Authorize(Policy = Policies.ApiWriter)]
    public async Task<ActionResult<OperatorDto>> Update(int id, OperatorInput input, CancellationToken ct)
    {
        var op = await db.Operators.FindAsync([id], ct);
        if (op is null) return NotFoundProblem($"Оператора {id} не знайдено");
        if (!CurrentUser.CanEdit(op.CommunityId)) return ForbiddenProblem("Оператор належить іншій громаді");

        Apply(input, op);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    [HttpDelete("{id:int}"), Authorize(Policy = Policies.ApiWriter)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var op = await db.Operators.Include(o => o.Points).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (op is null) return NotFoundProblem($"Оператора {id} не знайдено");
        if (!CurrentUser.CanEdit(op.CommunityId)) return ForbiddenProblem("Оператор належить іншій громаді");
        if (op.Points.Count > 0)
            return ConflictProblem($"За оператором закріплено пунктів: {op.Points.Count}. Спершу призначте їм іншого оператора.");

        db.Operators.Remove(op);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(OperatorInput input, Operator op)
    {
        op.Name = input.Name.Trim();
        op.Organization = input.Organization;
        op.Phone = input.Phone.Trim();
        op.Email = input.Email;
        op.Telegram = input.Telegram;
    }
}
