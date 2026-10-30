using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class ReportsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthorizedClient();

    private async Task<string> PointStatus(int pointId) =>
        (await (await _client.GetAsync($"/api/points/{pointId}")).ExpectJson(HttpStatusCode.OK))["status"]!.GetValue<string>();

    [Fact]
    public async Task New_report_sets_point_status()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());

        var r = await _client.PostJson("/api/reports", new
        {
            pointId, status = "Closed", hasElectricity = false, hasInternet = true, occupancy = 12, authorName = "Тест"
        });
        var json = await r.ExpectJson(HttpStatusCode.Created);

        Assert.Equal("Тестовий пункт", json["pointName"]!.GetValue<string>());
        Assert.True(json["hasInternet"]!.GetValue<bool>());
        Assert.Equal("Closed", await PointStatus(pointId));
    }

    [Fact]
    public async Task Editing_and_deleting_reports_recalculates_status()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        await _client.PostJson("/api/reports", new { pointId, status = "Limited" });
        await Task.Delay(20);
        var latest = await (await _client.PostJson("/api/reports", new { pointId, status = "Closed" }))
            .ExpectJson(HttpStatusCode.Created);
        var latestId = latest["id"]!.GetValue<int>();

        await (await _client.PutJson($"/api/reports/{latestId}", new { pointId, status = "Working" })).ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Working", await PointStatus(pointId));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/reports/{latestId}")).StatusCode);
        Assert.Equal("Limited", await PointStatus(pointId));
    }

    [Fact]
    public async Task Point_reports_endpoint_is_paged_and_newest_first()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        for (var i = 0; i < 3; i++)
        {
            await _client.PostJson("/api/reports", new { pointId, occupancy = i });
            await Task.Delay(20);
        }

        var page = await (await _client.GetAsync($"/api/points/{pointId}/reports?limit=2")).ExpectJson(HttpStatusCode.OK);
        var items = page["items"]!.AsArray();
        Assert.Equal(3, page["total"]!.GetValue<int>());
        Assert.Equal(2, items[0]!["occupancy"]!.GetValue<int>());
        Assert.Equal(1, items[1]!["occupancy"]!.GetValue<int>());
        Assert.NotNull(page["nextLink"]);
    }

    [Fact]
    public async Task Report_for_unknown_point_is_rejected()
    {
        var json = await (await _client.PostJson("/api/reports", new { pointId = 999999 })).ExpectJson(HttpStatusCode.BadRequest);
        Assert.True(json["errors"]!.AsObject().ContainsKey("PointId"));

        await (await _client.GetAsync("/api/points/999999/reports")).ExpectJson(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Report_photo_upload()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        var report = await (await _client.PostJson("/api/reports", new { pointId })).ExpectJson(HttpStatusCode.Created);
        var id = report["id"]!.GetValue<int>();

        using var form = new MultipartFormDataContent { { ApiClient.PngContent(), "file", "report.png" } };
        var json = await (await _client.PostAsync($"/api/reports/{id}/photo", form)).ExpectJson(HttpStatusCode.OK);
        Assert.Contains("/uploads/reports/", json["photoUrl"]!.GetValue<string>());

        await _client.DeleteAsync($"/api/points/{pointId}");
    }
}
