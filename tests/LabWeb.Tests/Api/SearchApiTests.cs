using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class SearchApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Code() => "k" + Guid.NewGuid().ToString("N")[..10];

    private async Task<int> CreatePoint(string name, string settlement, int[]? serviceIds = null, string status = "Working")
    {
        var body = ApiClient.PointBody(await _client.CreateOperator(), status, serviceIds, settlement, name);
        return (await (await _client.PostJson("/api/points", body)).ExpectJson(HttpStatusCode.Created))["id"]!.GetValue<int>();
    }

    private async Task<List<int>> Ids(string query, string extra = "") =>
        (await (await _client.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}&limit=50{extra}")).ExpectJson(HttpStatusCode.OK))["items"]!
            .AsArray().Select(i => i!["id"]!.GetValue<int>()).ToList();

    [Fact]
    public async Task Finds_by_word_prefixes_in_different_fields()
    {
        var code = Code();
        var id = await CreatePoint($"Бібліотека Кобзар {code}", "Овруч");

        Assert.Contains(id, await Ids($"кобз овру {code}"));
        Assert.DoesNotContain(id, await Ids($"кобзар харків {code}"));
    }

    [Fact]
    public async Task Tolerates_typos_and_word_endings()
    {
        var code = Code();
        var id = await CreatePoint($"Бібліотека {code}", "Овруч");

        Assert.Contains(id, await Ids($"бібліотеці {code}"));
        Assert.Contains(id, await Ids($"обруч {code}"));
    }

    [Fact]
    public async Task Index_follows_point_changes()
    {
        var code = Code();
        var operatorId = await _client.CreateOperator();
        var id = await _client.CreatePoint(operatorId);

        var renamed = ApiClient.PointBody(operatorId, name: $"Укриття {code}");
        await (await _client.PutJson($"/api/points/{id}", renamed)).ExpectJson(HttpStatusCode.OK);
        Assert.Contains(id, await Ids(code));

        await _client.DeleteAsync($"/api/points/{id}");
        Assert.Empty(await Ids(code));
    }

    [Fact]
    public async Task Renaming_service_reindexes_its_points()
    {
        var code = Code();
        var serviceId = await _client.CreateService($"Старлінк {code}");
        var id = await CreatePoint("Пункт зі старлінком", "Тестове", [serviceId]);
        Assert.Contains(id, await Ids(code));

        var newCode = Code();
        await (await _client.PutJson($"/api/services/{serviceId}", new { name = $"Супутник {newCode}", icon = "bi-star" }))
            .ExpectJson(HttpStatusCode.OK);

        Assert.Contains(id, await Ids($"супутник {newCode}"));
    }

    [Fact]
    public async Task Filters_by_status_and_pages_results()
    {
        var code = Code();
        var working = await CreatePoint($"Пункт {code} один", "Тестове");
        await CreatePoint($"Пункт {code} два", "Тестове");
        var closed = await CreatePoint($"Пункт {code} три", "Тестове", status: "Closed");

        Assert.Equal([closed], await Ids(code, "&status=Closed"));

        var page = await (await _client.GetAsync($"/api/search?q={code}&limit=2")).ExpectJson(HttpStatusCode.OK);
        Assert.Equal(3, page["total"]!.GetValue<int>());
        Assert.Equal(2, page["items"]!.AsArray().Count);
        Assert.NotNull(page["nextLink"]);
        Assert.Contains(working, await Ids(code));
    }

    [Theory]
    [InlineData("/api/search")]
    [InlineData("/api/search?q=а")]
    [InlineData("/api/search?q=ok&limit=500")]
    public async Task Invalid_query_is_rejected(string url)
    {
        var json = await (await _client.GetAsync(url)).ExpectJson(HttpStatusCode.BadRequest);
        Assert.NotNull(json["errors"]);
    }
}
