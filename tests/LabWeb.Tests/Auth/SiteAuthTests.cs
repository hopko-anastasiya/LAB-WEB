using System.Net;
using LabWeb.Auth;
using LabWeb.Models;
using LabWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LabWeb.Tests.Auth;

[Collection(ApiCollection.Name)]
public class SiteAuthTests(ApiFactory factory)
{
    private BrowserSession Browser() => new(factory);

    private async Task<int> CreatePointIn(TestUser user)
    {
        var client = factory.CreateClientWithToken(user.Token);
        return await client.CreatePoint(await client.CreateOperator());
    }

    [Fact]
    public async Task Anonymous_visitor_is_sent_to_login_with_return_url()
    {
        var response = await Browser().Client.GetAsync("/Points/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login?ReturnUrl=%2FPoints%2FCreate", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Public_pages_stay_open_without_login()
    {
        var client = Browser().Client;
        foreach (var url in new[] { "/", "/Map", "/Points", "/Reports", "/Operators", "/Services" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Google_button_starts_oauth_challenge()
    {
        var browser = Browser();

        var response = await browser.PostForm("/Account/Google", "/Account/Login", new() { ["returnUrl"] = "/Map" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        Assert.Contains("client_id=test-google-client", location.Query);
        Assert.Contains(Uri.EscapeDataString("/signin-google"), location.Query);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Correlation."));
    }

    [Fact]
    public async Task First_login_without_community_goes_to_onboarding()
    {
        var browser = Browser();
        await browser.SignIn($"new-{Guid.NewGuid():N}@tests.local");

        var response = await browser.Client.GetAsync("/Account/AfterLogin?returnUrl=%2FMap");

        Assert.Equal("/Community/Start?returnUrl=%2FMap", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Joining_by_invite_code_makes_user_a_member()
    {
        var admin = factory.CreateUser(communityName: "Громада з кодом");
        string code;
        await using (var db = factory.CreateDbContext())
            code = db.Communities.Single(c => c.Id == admin.CommunityId).InviteCode;

        var browser = Browser();
        var email = $"joiner-{Guid.NewGuid():N}@tests.local";
        await browser.SignIn(email);
        var wrong = await browser.PostForm("/Community/Join", "/Community/Start", new() { ["code"] = "NOPE1234" });
        var right = await browser.PostForm("/Community/Join", "/Community/Start", new() { ["code"] = code.ToLowerInvariant() });

        Assert.EndsWith("/Community/Start", wrong.Headers.Location!.OriginalString);
        Assert.EndsWith("/Community", right.Headers.Location!.OriginalString);
        await using var check = factory.CreateDbContext();
        var user = check.Users.Single(u => u.Email == email);
        Assert.Equal(admin.CommunityId, user.CommunityId);
        Assert.Equal(CommunityRole.Member, user.Role);
    }

    [Fact]
    public async Task Foreign_point_cannot_be_edited_on_site()
    {
        var owner = factory.CreateUser(communityName: "Власник");
        var pointId = await CreatePointIn(owner);
        var stranger = factory.CreateUser(communityName: "Сусіди");
        var browser = Browser();
        await browser.SignIn(stranger.Email);

        var edit = await browser.Client.GetAsync($"/Points/Edit/{pointId}");
        var delete = await browser.Client.GetAsync($"/Points/Delete/{pointId}");
        var details = await browser.Client.GetStringAsync($"/Points/Details/{pointId}");

        Assert.StartsWith("/Account/Denied", edit.Headers.Location!.PathAndQuery);
        Assert.StartsWith("/Account/Denied", delete.Headers.Location!.PathAndQuery);
        Assert.DoesNotContain($"/Points/Edit/{pointId}", details);
    }

    [Fact]
    public async Task Own_point_can_be_edited_on_site()
    {
        var owner = factory.CreateUser(communityName: "Свої");
        var pointId = await CreatePointIn(owner);
        var browser = Browser();
        await browser.SignIn(owner.Email);

        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync($"/Points/Edit/{pointId}")).StatusCode);
        Assert.Contains($"/Points/Edit/{pointId}", await browser.Client.GetStringAsync($"/Points/Details/{pointId}"));
    }

    [Fact]
    public async Task Analytics_is_premium_feature()
    {
        var free = factory.CreateUser(communityName: "Безкоштовна", plan: Plan.Free);
        var premium = factory.CreateUser(communityName: "Преміальна", plan: Plan.Premium);

        var freeBrowser = Browser();
        await freeBrowser.SignIn(free.Email);
        var denied = await freeBrowser.Client.GetAsync("/Analytics");

        var premiumBrowser = Browser();
        await premiumBrowser.SignIn(premium.Email);
        var allowed = await premiumBrowser.Client.GetAsync("/Analytics");

        Assert.Equal("/Community/Premium", denied.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("Аналітика громади", await allowed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Only_community_admin_changes_plan()
    {
        var admin = factory.CreateUser(communityName: "Громада плану", plan: Plan.Free);
        var member = factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId);

        var memberBrowser = Browser();
        await memberBrowser.SignIn(member.Email);
        var forbidden = await memberBrowser.PostForm("/Community/ChangePlan", "/Community/Premium", new() { ["plan"] = "Premium" });

        var adminBrowser = Browser();
        await adminBrowser.SignIn(admin.Email);
        await adminBrowser.PostForm("/Community/ChangePlan", "/Community/Premium", new() { ["plan"] = "Premium" });

        Assert.StartsWith("/Account/Denied", forbidden.Headers.Location!.PathAndQuery);
        await using var db = factory.CreateDbContext();
        Assert.Equal(Plan.Premium, db.Communities.Single(c => c.Id == admin.CommunityId).Plan);
        Assert.Equal(HttpStatusCode.OK, (await adminBrowser.Client.GetAsync("/Analytics")).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_session()
    {
        var user = factory.CreateUser(communityName: "Вихід");
        var browser = Browser();
        await browser.SignIn(user.Email);
        Assert.Equal(HttpStatusCode.OK, (await browser.Client.GetAsync("/Account/Profile")).StatusCode);

        await browser.PostForm("/Account/Logout", "/Account/Profile", new());

        Assert.Equal(HttpStatusCode.Redirect, (await browser.Client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Same_google_account_logs_into_same_user_and_links_existing_email()
    {
        var email = $"linked-{Guid.NewGuid():N}@tests.local";
        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccounts>();

        var devUser = await accounts.FindOrCreateAsync(new ExternalLogin(null, email, "Розробник", null));
        var first = await accounts.FindOrCreateAsync(new ExternalLogin("google-123-" + email, email.ToUpperInvariant(), "Google Ім'я", null));
        var second = await accounts.FindOrCreateAsync(new ExternalLogin("google-123-" + email, email, "Нове ім'я", null));

        Assert.Equal(devUser.Id, first.Id);
        Assert.Equal(first.Id, second.Id);
        await using var db = factory.CreateDbContext();
        var stored = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal("Нове ім'я", stored.Name);
        Assert.Equal("google-123-" + email, stored.GoogleId);
    }

    [Fact]
    public async Task Dev_login_is_hidden_outside_development()
    {
        using var production = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var client = production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var login = await client.GetStringAsync("/Account/Login");

        Assert.DoesNotContain("DevLogin", login);
        Assert.Contains("Увійти через Google", login);
    }
}
