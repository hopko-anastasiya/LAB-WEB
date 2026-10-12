using LabWeb.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabWeb.Controllers;

public class AccountController(UserAccounts accounts, CurrentUser currentUser, IConfiguration config, IWebHostEnvironment env) : Controller
{
    public IActionResult Login(string? returnUrl, string? error)
    {
        if (currentUser.IsSignedIn) return RedirectToAction(nameof(AfterLogin), new { returnUrl });

        ViewData["returnUrl"] = returnUrl;
        ViewData["google"] = AuthSetup.GoogleEnabled(config);
        ViewData["dev"] = AuthSetup.DevLoginEnabled(config, env);
        if (error == "google")
            ViewData["error"] = "Не вдалося увійти через Google. Спробуйте ще раз.";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Google(string? returnUrl)
    {
        if (!AuthSetup.GoogleEnabled(config)) return NotFound();
        var redirect = Url.Action(nameof(AfterLogin), new { returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirect }, "Google");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DevLogin(string email, string? name, string? returnUrl)
    {
        if (!AuthSetup.DevLoginEnabled(config, env)) return NotFound();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            TempData["Error"] = "Вкажіть email.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = await accounts.FindOrCreateAsync(new ExternalLogin(null, email, name ?? email, null));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserAccounts.Principal(user));
        return RedirectToAction(nameof(AfterLogin), new { returnUrl });
    }

    [Authorize]
    public IActionResult AfterLogin(string? returnUrl)
    {
        if (currentUser.CommunityId is null)
            return RedirectToAction("Start", "Community", new { returnUrl });
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public IActionResult Profile() => View(currentUser);

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateToken()
    {
        TempData["Token"] = await accounts.IssueApiTokenAsync(currentUser.User!.Id);
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeToken()
    {
        await accounts.RevokeApiTokenAsync(currentUser.User!.Id);
        TempData["Success"] = "Токен відкликано. Запити з ним більше не працюватимуть.";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult Denied() => View(currentUser);
}
