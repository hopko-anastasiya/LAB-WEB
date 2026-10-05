using LabWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

public class MapController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() =>
        View(await db.Services.AsNoTracking().OrderBy(s => s.Name).ToListAsync());
}
