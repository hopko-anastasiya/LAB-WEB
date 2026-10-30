using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Infrastructure;
using LabWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Controllers;

[Authorize]
public class CommunityController(AppDbContext db, CurrentUser currentUser) : Controller
{
    public IActionResult Start(string? returnUrl)
    {
        if (currentUser.CommunityId is not null) return RedirectToAction(nameof(Index));
        ViewData["returnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? returnUrl)
    {
        if (currentUser.CommunityId is not null) return RedirectToAction(nameof(Index));
        name = name?.Trim() ?? "";
        if (name.Length is < 3 or > 120)
        {
            TempData["Error"] = "Назва громади має містити від 3 до 120 символів.";
            return RedirectToAction(nameof(Start), new { returnUrl });
        }

        var community = new Community { Name = name, InviteCode = UserAccounts.NewInviteCode() };
        var user = await db.Users.FirstAsync(u => u.Id == currentUser.User!.Id);
        user.Community = community;
        user.Role = CommunityRole.Admin;
        await db.SaveChangesAsync();

        TempData["Success"] = $"Громаду «{name}» створено. Ви її адміністратор.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(string code, string? returnUrl)
    {
        if (currentUser.CommunityId is not null) return RedirectToAction(nameof(Index));
        var normalized = code?.Trim().ToUpperInvariant() ?? "";
        var community = await db.Communities.FirstOrDefaultAsync(c => c.InviteCode == normalized);
        if (community is null)
        {
            TempData["Error"] = "Такого коду запрошення немає. Перевірте його в адміністратора громади.";
            return RedirectToAction(nameof(Start), new { returnUrl });
        }

        var user = await db.Users.FirstAsync(u => u.Id == currentUser.User!.Id);
        user.CommunityId = community.Id;
        user.Role = CommunityRole.Member;
        await db.SaveChangesAsync();

        TempData["Success"] = $"Ви приєдналися до громади «{community.Name}».";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.Member)]
    public async Task<IActionResult> Index()
    {
        var community = await db.Communities.AsNoTracking()
            .Include(c => c.Users.OrderBy(u => u.Role).ThenBy(u => u.Name))
            .Include(c => c.Operators).ThenInclude(o => o.Points)
            .AsSplitQuery()
            .FirstAsync(c => c.Id == currentUser.CommunityId);
        return View(community);
    }

    [Authorize(Policy = Policies.CommunityAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewInviteCode()
    {
        var community = await db.Communities.FirstAsync(c => c.Id == currentUser.CommunityId);
        community.InviteCode = UserAccounts.NewInviteCode();
        await db.SaveChangesAsync();
        TempData["Success"] = "Створено новий код. Старий більше не діє.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.CommunityAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(int userId, CommunityRole role)
    {
        var member = await FindMember(userId);
        if (member is null) return NotFound();
        if (member.Id == currentUser.User!.Id)
        {
            TempData["Error"] = "Власну роль змінити не можна. Попросіть іншого адміністратора.";
            return RedirectToAction(nameof(Index));
        }
        member.Role = role;
        await db.SaveChangesAsync();
        TempData["Success"] = $"{member.Name}: тепер {role.DisplayName()}.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.CommunityAdmin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int userId)
    {
        var member = await FindMember(userId);
        if (member is null) return NotFound();
        if (member.Id == currentUser.User!.Id)
        {
            TempData["Error"] = "Щоб вийти самому, скористайтеся кнопкою «Вийти з громади».";
            return RedirectToAction(nameof(Index));
        }
        member.CommunityId = null;
        member.Role = CommunityRole.Member;
        await db.SaveChangesAsync();
        TempData["Success"] = $"{member.Name} більше не в громаді.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Policies.Member), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave()
    {
        var user = await db.Users.FirstAsync(u => u.Id == currentUser.User!.Id);
        var otherAdmins = await db.Users.AnyAsync(u => u.CommunityId == user.CommunityId && u.Id != user.Id && u.Role == CommunityRole.Admin);
        var others = await db.Users.AnyAsync(u => u.CommunityId == user.CommunityId && u.Id != user.Id);
        if (user.Role == CommunityRole.Admin && others && !otherAdmins)
        {
            TempData["Error"] = "Ви єдиний адміністратор. Спершу призначте адміністратором когось іншого.";
            return RedirectToAction(nameof(Index));
        }
        user.CommunityId = null;
        user.Role = CommunityRole.Member;
        await db.SaveChangesAsync();
        TempData["Success"] = "Ви вийшли з громади.";
        return RedirectToAction("Index", "Home");
    }


    private Task<AppUser?> FindMember(int userId) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CommunityId == currentUser.CommunityId);
}
