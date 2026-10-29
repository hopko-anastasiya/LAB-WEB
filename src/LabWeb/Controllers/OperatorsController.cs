using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class OperatorsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Operators.AsNoTracking().Include(o => o.Points).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.Name, term)
                                  || (o.Organization != null && EF.Functions.ILike(o.Organization, term)));
        }
        ViewData["q"] = q;
        return View(await query.OrderBy(o => o.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var op = await db.Operators.AsNoTracking()
            .Include(o => o.Points)
            .FirstOrDefaultAsync(o => o.Id == id);
        return op is null ? NotFound() : View(op);
    }

    public IActionResult Create() => View(new Operator());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Organization,Phone,Email,Telegram")] Operator op)
    {
        if (!ModelState.IsValid) return View(op);
        db.Add(op);
        await db.SaveChangesAsync();
        TempData["Success"] = "Оператора додано.";
        return RedirectToAction(nameof(Details), new { id = op.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var op = await db.Operators.FindAsync(id);
        return op is null ? NotFound() : View(op);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Organization,Phone,Email,Telegram")] Operator op)
    {
        if (id != op.Id) return NotFound();
        if (!ModelState.IsValid) return View(op);

        var existing = await db.Operators.FindAsync(id);
        if (existing is null) return NotFound();

        db.Entry(existing).CurrentValues.SetValues(op);
        await db.SaveChangesAsync();
        TempData["Success"] = "Зміни збережено.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var op = await db.Operators.AsNoTracking()
            .Include(o => o.Points)
            .FirstOrDefaultAsync(o => o.Id == id);
        return op is null ? NotFound() : View(op);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var op = await db.Operators.Include(o => o.Points).FirstOrDefaultAsync(o => o.Id == id);
        if (op is null) return NotFound();

        if (op.Points.Count > 0)
        {
            TempData["Error"] = "Неможливо видалити оператора, за яким закріплені пункти. Спершу передайте їх іншому оператору.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        db.Operators.Remove(op);
        await db.SaveChangesAsync();
        TempData["Success"] = "Оператора видалено.";
        return RedirectToAction(nameof(Index));
    }
}
