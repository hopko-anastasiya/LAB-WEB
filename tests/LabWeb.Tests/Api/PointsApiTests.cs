using System.Net;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class PointsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthorizedClient();

    [Fact]
    public async Task Create_returns_point_with_operator_and_services()
    {
        var operatorId = await _client.CreateOperator();
        var serviceId = await _client.CreateService();

        var r = await _client.PostJson("/api/points", ApiClient.PointBody(operatorId, serviceIds: [serviceId, serviceId]));
        var json = await r.ExpectJson(HttpStatusCode.Created);

        Assert.Equal(operatorId, json["operator"]!["id"]!.GetValue<int>());
        Assert.Single(json["services"]!.AsArray());
        Assert.Equal("Working", json["status"]!.GetValue<string>());
        Assert.Null(json["photoUrl"]);
    }

    [Fact]
    public async Task Update_replaces_fields_and_services()
    {
        var operatorId = await _client.CreateOperator();
        var first = await _client.CreateService();
        var second = await _client.CreateService();
        var id = await _client.CreatePoint(operatorId, serviceIds: [first]);

        var body = ApiClient.PointBody(operatorId, "Closed", [second], "Одеса", "Оновлений пункт");
        var json = await (await _client.PutJson($"/api/points/{id}", body)).ExpectJson(HttpStatusCode.OK);

        Assert.Equal("Оновлений пункт", json["name"]!.GetValue<string>());
        Assert.Equal("Одеса", json["settlement"]!.GetValue<string>());
        Assert.Equal("Closed", json["status"]!.GetValue<string>());
        Assert.Equal(second, json["services"]!.AsArray().Single()!["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task Filters_by_service_and_status()
    {
        var operatorId = await _client.CreateOperator();
        var serviceId = await _client.CreateService();
        var withService = await _client.CreatePoint(operatorId, "Limited", [serviceId]);
        await _client.CreatePoint(operatorId, "Limited");

        var byService = await (await _client.GetAsync($"/api/points?serviceId={serviceId}")).ExpectJson(HttpStatusCode.OK);
        Assert.Equal(withService, byService["items"]!.AsArray().Single()!["id"]!.GetValue<int>());

        var byStatus = await (await _client.GetAsync("/api/points?status=Limited&limit=100")).ExpectJson(HttpStatusCode.OK);
        Assert.All(byStatus["items"]!.AsArray(), p => Assert.Equal("Limited", p!["status"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Validation_errors_for_coordinates_operator_and_status()
    {
        var operatorId = await _client.CreateOperator();

        var badLat = await (await _client.PostJson("/api/points", new { name = "Х", address = "Х", settlement = "Х", latitude = 120, operatorId }))
            .ExpectJson(HttpStatusCode.BadRequest);
        Assert.True(badLat["errors"]!.AsObject().ContainsKey("Latitude"));

        var badOperator = await (await _client.PostJson("/api/points", ApiClient.PointBody(999999)))
            .ExpectJson(HttpStatusCode.BadRequest);
        Assert.True(badOperator["errors"]!.AsObject().ContainsKey("OperatorId"));

        var badStatus = await (await _client.PostJson("/api/points", ApiClient.PointBody(operatorId, status: "Sleeping")))
            .ExpectJson(HttpStatusCode.BadRequest);
        Assert.Equal("Дані запиту некоректні", badStatus["title"]!.GetValue<string>());
        Assert.True(badStatus["errors"]!.AsObject().ContainsKey("status"));
    }

    [Fact]
    public async Task Photo_upload_replace_and_delete()
    {
        var id = await _client.CreatePoint(await _client.CreateOperator());

        using var form = new MultipartFormDataContent { { ApiClient.PngContent(), "file", "photo.png" } };
        var uploaded = await (await _client.PostAsync($"/api/points/{id}/photo", form)).ExpectJson(HttpStatusCode.OK);
        var photoUrl = uploaded["photoUrl"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(photoUrl)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/points/{id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(photoUrl)).StatusCode);
        var after = await (await _client.GetAsync($"/api/points/{id}")).ExpectJson(HttpStatusCode.OK);
        Assert.Null(after["photoUrl"]);

        await _client.DeleteAsync($"/api/points/{id}");
    }

    [Fact]
    public async Task Photo_must_be_an_image()
    {
        var id = await _client.CreatePoint(await _client.CreateOperator());

        using var text = new StringContent("not an image");
        text.Headers.ContentType = new("text/plain");
        using var form = new MultipartFormDataContent { { text, "file", "note.txt" } };
        var json = await (await _client.PostAsync($"/api/points/{id}/photo", form)).ExpectJson(HttpStatusCode.BadRequest);
        Assert.True(json["errors"]!.AsObject().ContainsKey("file"));

        using var empty = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        await (await _client.PostAsync($"/api/points/{id}/photo", empty)).ExpectJson(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_point_removes_its_reports()
    {
        var id = await _client.CreatePoint(await _client.CreateOperator());
        var report = await (await _client.PostJson("/api/reports", new { pointId = id, status = "Working" }))
            .ExpectJson(HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/points/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/points/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/reports/{report["id"]}")).StatusCode);
    }
}
