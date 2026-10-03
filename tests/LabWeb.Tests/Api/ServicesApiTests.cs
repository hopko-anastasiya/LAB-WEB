using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class ServicesApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_get_update_delete()
    {
        var created = await _client.PostJson("/api/services", new { name = "Душ", icon = "bi-droplet-half", description = "Гаряча вода" });
        var json = await created.ExpectJson(HttpStatusCode.Created);
        var id = json["id"]!.GetValue<int>();
        Assert.EndsWith($"/api/services/{id}", created.Headers.Location!.ToString());

        var got = await (await _client.GetAsync($"/api/services/{id}")).ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Душ", got["name"]!.GetValue<string>());
        Assert.Equal(0, got["pointsCount"]!.GetValue<int>());

        var updated = await (await _client.PutJson($"/api/services/{id}", new { name = "Душова", icon = "bi-droplet" }))
            .ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Душова", updated["name"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/services/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/services/{id}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_is_conflict_case_insensitive()
    {
        await _client.CreateService("Пральня");
        var r = await _client.PostJson("/api/services", new { name = "ПРАЛЬНЯ" });
        var json = await r.ExpectJson(HttpStatusCode.Conflict);
        Assert.Contains("вже існує", json["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invalid_input_lists_field_errors()
    {
        var json = await (await _client.PostJson("/api/services", new { name = "", icon = "wrong icon" }))
            .ExpectJson(HttpStatusCode.BadRequest);
        var errors = json["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("Name"));
        Assert.True(errors.ContainsKey("Icon"));
    }

    [Fact]
    public async Task Unknown_id_returns_problem_404()
    {
        var r = await _client.PutJson("/api/services/999999", new { name = "Х" });
        var json = await r.ExpectJson(HttpStatusCode.NotFound);
        Assert.Equal(404, json["status"]!.GetValue<int>());
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType!.MediaType);
    }
}
