using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class LookupApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Settlements_are_loaded_from_geonames()
    {
        await using var db = factory.CreateDbContext();
        Assert.True(db.Settlements.Count() > 30_000);
    }

    [Fact]
    public async Task Settlement_search_puts_exact_and_bigger_matches_first()
    {
        var items = (await (await _client.GetAsync("/api/lookup/settlements?q=Київ&limit=5")).ExpectJson(HttpStatusCode.OK)).AsArray();

        Assert.Equal("Київ", items[0]!["name"]!.GetValue<string>());
        Assert.InRange(items[0]!["latitude"]!.GetValue<double>(), 50.3, 50.6);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public async Task Settlement_search_matches_every_word_in_name_or_region()
    {
        var items = (await (await _client.GetAsync("/api/lookup/settlements?q=біла київська")).ExpectJson(HttpStatusCode.OK)).AsArray();

        Assert.Contains(items, i => i!["name"]!.GetValue<string>() == "Біла Церква");
        Assert.All(items, i => Assert.Equal("Київська область", i!["region"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("/api/lookup/settlements?q=Ки")]
    [InlineData("/api/lookup/operators?q=")]
    [InlineData("/api/lookup/points?q=ab&limit=100")]
    public async Task Short_query_is_rejected(string url)
    {
        var json = await (await _client.GetAsync(url)).ExpectJson(HttpStatusCode.BadRequest);
        Assert.NotNull(json["errors"]);
    }

    [Fact]
    public async Task Operators_and_points_are_found_by_any_field()
    {
        var operatorId = await _client.CreateOperator("Марта Гаврилюк");
        var pointId = await _client.CreatePoint(operatorId, settlement: "Немирів");

        var operators = (await (await _client.GetAsync("/api/lookup/operators?q=гаври")).ExpectJson(HttpStatusCode.OK)).AsArray();
        var points = (await (await _client.GetAsync("/api/lookup/points?q=немир")).ExpectJson(HttpStatusCode.OK)).AsArray();

        Assert.Contains(operators, o => o!["id"]!.GetValue<int>() == operatorId);
        var point = Assert.Single(points, p => p!["id"]!.GetValue<int>() == pointId)!;
        Assert.Equal("Немирів, вул. Тестова, 1", point["hint"]!.GetValue<string>());
    }

    [Fact]
    public async Task Percent_sign_is_searched_literally()
    {
        var items = (await (await _client.GetAsync("/api/lookup/settlements?q=%25%25%25")).ExpectJson(HttpStatusCode.OK)).AsArray();
        Assert.Empty(items);
    }
}
