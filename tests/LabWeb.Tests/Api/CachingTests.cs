using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class CachingTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Repeated_get_is_served_from_cache()
    {
        var url = $"/api/services?limit=7&probe={Guid.NewGuid():N}";
        var first = await _client.GetAsync(url);
        var second = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Null(first.Headers.Age);
        Assert.NotNull(second.Headers.Age);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Changes_evict_cached_responses()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        var url = $"/api/points/{pointId}";
        await _client.GetAsync(url);
        Assert.NotNull((await _client.GetAsync(url)).Headers.Age);

        await (await _client.PostJson("/api/reports", new { pointId, status = "Closed" })).ExpectJson(HttpStatusCode.Created);

        var fresh = await _client.GetAsync(url);
        Assert.Null(fresh.Headers.Age);
        Assert.Equal("Closed", (await fresh.ExpectJson(HttpStatusCode.OK))["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Writes_and_errors_are_not_cached()
    {
        var missing = "/api/points/987654";
        await _client.GetAsync(missing);
        Assert.Null((await _client.GetAsync(missing)).Headers.Age);

        var body = new { name = $"Послуга {Guid.NewGuid():N}", icon = "bi-star" };
        Assert.Equal(HttpStatusCode.Created, (await _client.PostJson("/api/services", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostJson("/api/services", body)).StatusCode);
    }

    [Fact]
    public async Task Settlements_stay_cached_after_data_changes()
    {
        var url = "/api/lookup/settlements?q=Одеса&limit=3";
        await _client.GetAsync(url);
        await _client.CreateService();

        Assert.NotNull((await _client.GetAsync(url)).Headers.Age);
    }
}
