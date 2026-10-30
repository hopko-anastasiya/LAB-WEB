using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class OperatorsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthorizedClient();

    [Fact]
    public async Task Create_update_and_search()
    {
        var id = await _client.CreateOperator("Марія Шевчук");

        var updated = await (await _client.PutJson($"/api/operators/{id}",
                new { name = "Марія Шевчук", organization = "Волонтери Дарниці", phone = "+380671234567", email = "maria@example.com" }))
            .ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Волонтери Дарниці", updated["organization"]!.GetValue<string>());

        var found = await (await _client.GetAsync("/api/operators?q=дарниці")).ExpectJson(HttpStatusCode.OK);
        Assert.Contains(found["items"]!.AsArray(), o => o!["id"]!.GetValue<int>() == id);
    }

    [Fact]
    public async Task Invalid_phone_and_email_are_rejected()
    {
        var json = await (await _client.PostJson("/api/operators", new { name = "Х", phone = "телефон", email = "not-an-email" }))
            .ExpectJson(HttpStatusCode.BadRequest);
        var errors = json["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("Phone"));
        Assert.True(errors.ContainsKey("Email"));
    }

    [Fact]
    public async Task Operator_with_points_cannot_be_deleted()
    {
        var id = await _client.CreateOperator();
        var pointId = await _client.CreatePoint(id);

        await (await _client.DeleteAsync($"/api/operators/{id}")).ExpectJson(HttpStatusCode.Conflict);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/points/{pointId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/operators/{id}")).StatusCode);
    }
}
