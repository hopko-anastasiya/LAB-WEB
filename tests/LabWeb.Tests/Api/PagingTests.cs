using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class PagingTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_returns_page_envelope_with_defaults()
    {
        var json = await (await _client.GetAsync("/api/points")).ExpectJson(HttpStatusCode.OK);

        Assert.Equal(0, json["skip"]!.GetValue<int>());
        Assert.Equal(20, json["limit"]!.GetValue<int>());
        Assert.True(json["total"]!.GetValue<int>() >= json["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task Walking_nextLink_returns_every_item_once()
    {
        var operatorId = await _client.CreateOperator();
        for (var i = 0; i < 5; i++)
            await _client.CreatePoint(operatorId, settlement: "Пагінаційне");

        var ids = new List<int>();
        string? url = "/api/points?settlement=Пагінаційне&limit=2";
        var pages = 0;
        int total = 0;
        while (url is not null)
        {
            var page = await (await _client.GetAsync(url)).ExpectJson(HttpStatusCode.OK);
            total = page["total"]!.GetValue<int>();
            ids.AddRange(page["items"]!.AsArray().Select(x => x!["id"]!.GetValue<int>()));
            url = page["nextLink"]?.GetValue<string>();
            pages++;
            Assert.True(pages <= 10, "nextLink зациклився");
        }

        Assert.Equal(5, total);
        Assert.Equal(3, pages);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(ids.Order(), ids);
    }

    [Fact]
    public async Task NextLink_keeps_filters_and_moves_skip()
    {
        var operatorId = await _client.CreateOperator();
        for (var i = 0; i < 3; i++)
            await _client.CreatePoint(operatorId, status: "Limited", settlement: "Фільтроване");

        var page = await (await _client.GetAsync("/api/points?settlement=Фільтроване&status=Limited&limit=1"))
            .ExpectJson(HttpStatusCode.OK);
        var next = new Uri(page["nextLink"]!.GetValue<string>());
        var query = System.Web.HttpUtility.ParseQueryString(next.Query);

        Assert.Equal("/api/points", next.AbsolutePath);
        Assert.Equal("Фільтроване", query["settlement"]);
        Assert.Equal("Limited", query["status"]);
        Assert.Equal("1", query["skip"]);
        Assert.Equal("1", query["limit"]);
    }

    [Fact]
    public async Task Last_page_has_no_nextLink()
    {
        var page = await (await _client.GetAsync("/api/services?limit=100")).ExpectJson(HttpStatusCode.OK);
        Assert.Null(page["nextLink"]);
    }

    [Fact]
    public async Task Skip_past_the_end_returns_empty_page()
    {
        var page = await (await _client.GetAsync("/api/points?skip=100000")).ExpectJson(HttpStatusCode.OK);
        Assert.Empty(page["items"]!.AsArray());
        Assert.Null(page["nextLink"]);
    }

    [Theory]
    [InlineData("/api/points?limit=0")]
    [InlineData("/api/points?limit=101")]
    [InlineData("/api/points?skip=-1")]
    [InlineData("/api/reports?limit=abc")]
    public async Task Invalid_paging_parameters_return_400(string url)
    {
        var response = await _client.GetAsync(url);
        var json = await response.ExpectJson(HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(json["errors"]);
    }
}
