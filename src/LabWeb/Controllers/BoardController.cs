using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models.ViewModels;
using LabWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement.Mvc;

namespace LabWeb.Controllers;

[Authorize(Policy = Policies.Member)]
[FeatureGate(Features.SharedBoard)]
public class BoardController(AppDbContext db, CurrentUser currentUser) : Controller
{
    public async Task<IActionResult> Index() =>
        View(new BoardViewModel
        {
            CommunityName = currentUser.Community!.Name,
            IsAdmin = currentUser.IsCommunityAdmin,
            Points = await db.Points.AsNoTracking()
                .Where(p => p.Operator!.CommunityId == currentUser.CommunityId)
                .OrderBy(p => p.Name)
                .ToMapPoints()
                .ToListAsync()
        });
}
