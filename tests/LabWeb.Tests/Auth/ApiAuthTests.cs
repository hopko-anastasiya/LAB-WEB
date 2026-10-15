using System.Net;
using System.Net.Http.Headers;
using LabWeb.Auth;
using LabWeb.Models;
using LabWeb.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LabWeb.Tests.Auth;

[Collection(ApiCollection.Name)]
public class ApiAuthTests(ApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    private (HttpClient Client, TestUser User) NewCommunityAdmin(Plan plan = Plan.Free)
    {
        var user = factory.CreateUser(communityName: $"Громада {Guid.NewGuid():N}"[..20], plan: plan);
        return (factory.CreateClientWithToken(user.Token), user);
    }

    [Fact]
    public async Task Reading_api_needs_no_token()
    {
        await (await _anonymous.GetAsync("/api/points")).ExpectJson(HttpStatusCode.OK);
        await (await _anonymous.GetAsync("/api/map/points")).ExpectJson(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer ")]
    [InlineData("Bearer not-a-real-token")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task Writing_without_valid_token_is_unauthorized(string? header)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/operators")
        {
            Content = JsonContent(new { name = "Хтось", phone = "+380501234567" })
        };
        if (header is not null)
            request.Headers.TryAddWithoutValidation("Authorization", header);

        var response = await _anonymous.SendAsync(request);

        var json = await response.ExpectJson(HttpStatusCode.Unauthorized);
        Assert.Equal("Потрібна автентифікація", json["title"]!.GetValue<string>());
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task User_without_community_cannot_write()
    {
        var user = factory.CreateUser();
        var client = factory.CreateClientWithToken(user.Token);

        var response = await client.PostJson("/api/operators", new { name = "Без громади", phone = "+380501234567" });

        await response.ExpectJson(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Other_community_cannot_change_points_operators_or_reports()
    {
        var (owner, _) = NewCommunityAdmin();
        var (stranger, _) = NewCommunityAdmin();
        var operatorId = await owner.CreateOperator();
        var pointId = await owner.CreatePoint(operatorId);
        var report = await (await owner.PostJson("/api/reports", new { pointId })).ExpectJson(HttpStatusCode.Created);
        var reportId = report["id"]!.GetValue<int>();

        var attempts = new[]
        {
            await stranger.PutJson($"/api/points/{pointId}", ApiClient.PointBody(operatorId, name: "Чужий")),
            await stranger.DeleteAsync($"/api/points/{pointId}"),
            await stranger.PostJson("/api/points", ApiClient.PointBody(operatorId)),
            await stranger.PostJson("/api/reports", new { pointId, status = "Closed" }),
            await stranger.PutJson($"/api/reports/{reportId}", new { pointId, status = "Closed" }),
            await stranger.DeleteAsync($"/api/reports/{reportId}"),
            await stranger.PutJson($"/api/operators/{operatorId}", new { name = "Чужий", phone = "+380501234567" }),
            await stranger.DeleteAsync($"/api/operators/{operatorId}"),
            await stranger.PostAsync($"/api/points/{pointId}/photo", new MultipartFormDataContent { { ApiClient.PngContent(), "file", "x.png" } })
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        var point = await (await _anonymous.GetAsync($"/api/points/{pointId}")).ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Тестовий пункт", point["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Operators_are_created_in_callers_community()
    {
        var (client, user) = NewCommunityAdmin();

        var json = await (await client.PostJson("/api/operators", new { name = "Новий", phone = "+380501234567" }))
            .ExpectJson(HttpStatusCode.Created);

        Assert.Equal(user.CommunityId, json["communityId"]!.GetValue<int>());
    }

    [Fact]
    public async Task Only_system_admin_edits_services_catalog()
    {
        var (communityAdmin, _) = NewCommunityAdmin(Plan.Premium);
        var body = new { name = $"Послуга {Guid.NewGuid():N}", icon = "bi-star" };

        await (await communityAdmin.PostJson("/api/services", body)).ExpectJson(HttpStatusCode.Forbidden);
        await (await factory.CreateAuthorizedClient().PostJson("/api/services", body)).ExpectJson(HttpStatusCode.Created);
    }

    [Fact]
    public async Task New_token_replaces_old_and_revoked_token_stops_working()
    {
        var (client, user) = NewCommunityAdmin();
        await (await client.PostJson("/api/operators", new { name = "До", phone = "+380501234567" })).ExpectJson(HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccounts>();
        var fresh = await accounts.IssueApiTokenAsync(user.Id);

        await (await client.PostJson("/api/operators", new { name = "Після", phone = "+380501234567" })).ExpectJson(HttpStatusCode.Unauthorized);
        var freshClient = factory.CreateClientWithToken(fresh);
        await (await freshClient.PostJson("/api/operators", new { name = "Новий токен", phone = "+380501234567" })).ExpectJson(HttpStatusCode.Created);

        await accounts.RevokeApiTokenAsync(user.Id);
        await (await freshClient.PostJson("/api/operators", new { name = "Відкликано", phone = "+380501234567" })).ExpectJson(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tokens_are_stored_only_as_hashes()
    {
        var (_, user) = NewCommunityAdmin();
        await using var db = factory.CreateDbContext();

        var stored = db.Users.Single(u => u.Id == user.Id).ApiTokenHash;

        Assert.NotEqual(user.Token, stored);
        Assert.Equal(UserAccounts.HashToken(user.Token), stored);
    }

    [Fact]
    public async Task Site_cookie_is_not_accepted_for_api_writes()
    {
        var user = factory.CreateUser(communityName: "Громада з cookie");
        var browser = new BrowserSession(factory);
        await browser.SignIn(user.Email);

        var response = await browser.Client.PostJson("/api/operators", new { name = "Через cookie", phone = "+380501234567" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonContent(object body) =>
        new(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
}
