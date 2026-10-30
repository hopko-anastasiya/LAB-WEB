using System.Text.Encodings.Web;
using LabWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LabWeb.Auth;

public class ApiTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0)
            return AuthenticateResult.Fail("Порожній токен");

        var hash = UserAccounts.HashToken(token);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ApiTokenHash == hash, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("Невідомий токен");

        return AuthenticateResult.Success(new AuthenticationTicket(UserAccounts.Principal(user, SchemeName), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return WriteProblem(StatusCodes.Status401Unauthorized, "Потрібна автентифікація",
            "Додайте заголовок Authorization: Bearer <токен>. Токен можна створити в профілі на сайті.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblem(StatusCodes.Status403Forbidden, "Доступ заборонено",
            "Щоб змінювати дані, приєднайтеся до громади або створіть її на сайті.");

    private Task WriteProblem(int status, string title, string detail)
    {
        Response.StatusCode = status;
        return Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Detail = detail },
            options: null, contentType: "application/problem+json");
    }
}
