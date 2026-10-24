using System.Security.Claims;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Auth;

public class CurrentUser(IConfiguration config)
{
    public AppUser? User { get; private set; }
    public int UnreadNotifications { get; private set; }

    public bool IsSignedIn => User is not null;
    public int? CommunityId => User?.CommunityId;
    public Community? Community => User?.Community;
    public bool IsCommunityAdmin => User is { CommunityId: not null, Role: CommunityRole.Admin };
    public bool IsPremium => Community?.Plan == Plan.Premium;

    public bool IsSystemAdmin =>
        User is not null
        && (config.GetSection("Auth:SystemAdmins").Get<string[]>() ?? [])
            .Contains(User.Email, StringComparer.OrdinalIgnoreCase);

    public bool CanEdit(int? communityId) => communityId is not null && communityId == CommunityId;

    public async Task LoadAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct = default)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return;
        User = await db.Users.AsNoTracking().Include(u => u.Community).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (User is not null)
            UnreadNotifications = await db.Notifications.CountAsync(n => n.UserId == id && n.ReadAt == null, ct);
    }
}

public class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            await currentUser.LoadAsync(context.User, db, context.RequestAborted);
        await next(context);
    }
}
