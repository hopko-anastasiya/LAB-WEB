using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace LabWeb.Auth;

public static class Policies
{
    public const string Member = "Member";
    public const string CommunityAdmin = "CommunityAdmin";
    public const string SystemAdmin = "SystemAdmin";
    public const string ApiWriter = "ApiWriter";
    public const string ApiSystemAdmin = "ApiSystemAdmin";
}

public enum Access { Member, CommunityAdmin, SystemAdmin }

public record AccessRequirement(Access Access) : IAuthorizationRequirement;

public class AccessHandler(CurrentUser currentUser) : AuthorizationHandler<AccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        var sameUser = currentUser.User?.Id.ToString() == context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var allowed = sameUser && requirement.Access switch
        {
            Access.Member => currentUser.CommunityId is not null,
            Access.CommunityAdmin => currentUser.IsCommunityAdmin,
            Access.SystemAdmin => currentUser.IsSystemAdmin,
            _ => false
        };
        if (allowed) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class AuthSetup
{
    private const string SmartScheme = "CookieOrToken";

    public static bool GoogleEnabled(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Auth:Google:ClientId"]) && !string.IsNullOrWhiteSpace(config["Auth:Google:ClientSecret"]);

    public static bool DevLoginEnabled(IConfiguration config, IWebHostEnvironment env) =>
        env.IsDevelopment() && config.GetValue<bool>("Auth:DevLogin");

    public static void AddAppAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        services.AddScoped<CurrentUser>();
        services.AddScoped<UserAccounts>();
        services.AddScoped<IAuthorizationHandler, AccessHandler>();

        var auth = services.AddAuthentication(o =>
            {
                o.DefaultScheme = SmartScheme;
                o.DefaultChallengeScheme = SmartScheme;
            })
            .AddPolicyScheme(SmartScheme, "Cookie або токен", o =>
                o.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? ApiTokenHandler.SchemeName
                        : CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "nezlamni.auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.LoginPath = "/Account/Login";
                o.AccessDeniedPath = "/Account/Denied";
                o.ExpireTimeSpan = TimeSpan.FromDays(14);
                o.SlidingExpiration = true;
                o.Events.OnRedirectToLogin = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/hubs")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    else ctx.Response.Redirect(ctx.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(ApiTokenHandler.SchemeName, null);

        if (GoogleEnabled(config))
        {
            auth.AddGoogle(o =>
            {
                o.ClientId = config["Auth:Google:ClientId"]!;
                o.ClientSecret = config["Auth:Google:ClientSecret"]!;
                o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.ClaimActions.MapJsonKey("picture", "picture");
                o.ClaimActions.MapJsonKey("email_verified", "email_verified");
                o.Events.OnCreatingTicket = async ctx =>
                {
                    var principal = ctx.Principal!;
                    var email = principal.FindFirstValue(ClaimTypes.Email);
                    if (string.IsNullOrEmpty(email) || principal.FindFirstValue("email_verified") is "false" or "False")
                    {
                        ctx.Fail("Google не підтвердив email цього акаунта.");
                        return;
                    }
                    var accounts = ctx.HttpContext.RequestServices.GetRequiredService<UserAccounts>();
                    var user = await accounts.FindOrCreateAsync(new ExternalLogin(
                        principal.FindFirstValue(ClaimTypes.NameIdentifier), email,
                        principal.FindFirstValue(ClaimTypes.Name) ?? email, principal.FindFirstValue("picture")));
                    ctx.Principal = UserAccounts.Principal(user);
                };
                o.Events.OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/Account/Login?error=google");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Member, p => p.RequireAuthenticatedUser().AddRequirements(new AccessRequirement(Access.Member)))
            .AddPolicy(Policies.CommunityAdmin, p => p.RequireAuthenticatedUser().AddRequirements(new AccessRequirement(Access.CommunityAdmin)))
            .AddPolicy(Policies.SystemAdmin, p => p.RequireAuthenticatedUser().AddRequirements(new AccessRequirement(Access.SystemAdmin)))
            .AddPolicy(Policies.ApiWriter, p => p
                .AddAuthenticationSchemes(ApiTokenHandler.SchemeName)
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRequirement(Access.Member)))
            .AddPolicy(Policies.ApiSystemAdmin, p => p
                .AddAuthenticationSchemes(ApiTokenHandler.SchemeName)
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRequirement(Access.SystemAdmin)));
    }
}
