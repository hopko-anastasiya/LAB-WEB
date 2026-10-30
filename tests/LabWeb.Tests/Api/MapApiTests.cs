using System.Net;
using System.Text.Json.Nodes;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class MapApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<JsonArray> MapPoints(string query = "") =>
        (await (await _client.GetAsync($"/api/map/points{query}")).ExpectJson(HttpStatusCode.OK)).AsArray();

    [Fact]
    public async Task Map_point_carries_latest_report_flags()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        await _client.PostJson("/api/reports", new { pointId, status = "Working", hasElectricity = true, occupancy = 3 });
        await Task.Delay(20);
        await _client.PostJson("/api/reports", new { pointId, status = "Limited", hasInternet = true, occupancy = 7 });

        var point = (await MapPoints()).Single(p => p!["id"]!.GetValue<int>() == pointId)!;

        Assert.Equal("Limited", point["status"]!.GetValue<string>());
        Assert.Equal(7, point["occupancy"]!.GetValue<int>());
        Assert.False(point["hasElectricity"]!.GetValue<bool>());
        Assert.True(point["hasInternet"]!.GetValue<bool>());
        Assert.Equal(50.45, point["latitude"]!.GetValue<double>());
    }

    [Fact]
    public async Task Map_filters_by_all_requested_services()
    {
        var operatorId = await _client.CreateOperator();
        var wifi = await _client.CreateService();
        var heat = await _client.CreateService();
        var both = await _client.CreatePoint(operatorId, serviceIds: [wifi, heat]);
        var onlyWifi = await _client.CreatePoint(operatorId, serviceIds: [wifi]);

        var ids = (await MapPoints($"?serviceId={wifi}&serviceId={heat}")).Select(p => p!["id"]!.GetValue<int>()).ToList();

        Assert.Contains(both, ids);
        Assert.DoesNotContain(onlyWifi, ids);
    }

    [Fact]
    public async Task Point_without_reports_has_no_flags()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator(), status: "Closed");

        var point = (await MapPoints("?status=Closed")).Single(p => p!["id"]!.GetValue<int>() == pointId)!;

        Assert.Null(point["occupancy"]);
        Assert.Null(point["hasElectricity"]);
    }
}
