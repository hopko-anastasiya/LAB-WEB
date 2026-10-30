using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LabWeb.Tests.Infrastructure;

public static class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<JsonNode> ReadJson(this HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())
        ?? throw new InvalidOperationException("Порожня відповідь");

    public static Task<HttpResponseMessage> PostJson(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJson(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static async Task<JsonNode> ExpectJson(this HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Очікували {(int)status}, отримали {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    public static async Task<int> CreateOperator(this HttpClient client, string name = "Тестовий оператор")
    {
        var r = await client.PostJson("/api/operators", new { name, phone = "+380501234567" });
        return (await r.ExpectJson(HttpStatusCode.Created))["id"]!.GetValue<int>();
    }

    public static async Task<int> CreateService(this HttpClient client, string? name = null)
    {
        var r = await client.PostJson("/api/services", new { name = name ?? $"Послуга {Guid.NewGuid():N}", icon = "bi-star" });
        return (await r.ExpectJson(HttpStatusCode.Created))["id"]!.GetValue<int>();
    }

    public static async Task<int> CreatePoint(this HttpClient client, int operatorId, string status = "Working",
        int[]? serviceIds = null, string settlement = "Тестове")
    {
        var r = await client.PostJson("/api/points", PointBody(operatorId, status, serviceIds, settlement));
        return (await r.ExpectJson(HttpStatusCode.Created))["id"]!.GetValue<int>();
    }

    public static object PointBody(int operatorId, string status = "Working", int[]? serviceIds = null,
        string settlement = "Тестове", string name = "Тестовий пункт") => new
    {
        name,
        address = "вул. Тестова, 1",
        settlement,
        latitude = 50.45,
        longitude = 30.52,
        capacity = 20,
        status,
        operatorId,
        serviceIds = serviceIds ?? []
    };

    public static ByteArrayContent PngContent()
    {
        byte[] png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        var content = new ByteArrayContent(png);
        content.Headers.ContentType = new("image/png");
        return content;
    }
}
