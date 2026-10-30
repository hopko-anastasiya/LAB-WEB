using System.Net.Http.Headers;
using LabWeb.Tests.Infrastructure;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class McpServerTests(ApiFactory factory)
{
    private async Task<McpClient> Connect(string? token = null)
    {
        var http = factory.CreateClient();
        if (token is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(factory.Server.BaseAddress, "mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, http);
        return await McpClient.CreateAsync(transport);
    }

    private static string Text(CallToolResult result) => ((TextContentBlock)result.Content[0]).Text;

    [Fact]
    public async Task Server_lists_read_and_write_tools()
    {
        await using var client = await Connect();

        var tools = await client.ListToolsAsync();

        Assert.Superset(
            new HashSet<string> { "search_points", "find_nearby_points", "get_point", "get_point_reports", "list_services", "add_status_report" },
            tools.Select(t => t.Name).ToHashSet());
    }

    [Fact]
    public async Task Read_tools_work_without_token()
    {
        await using var client = await Connect();

        var services = await client.CallToolAsync("list_services");
        var nearby = await client.CallToolAsync("find_nearby_points", new Dictionary<string, object?>
        {
            ["settlement"] = "Харків", ["services"] = new[] { "генератор" }
        });

        Assert.Contains("Генератор", Text(services));
        Assert.Contains("/punkt/", Text(nearby));
        Assert.NotEqual(true, nearby.IsError);
    }

    [Fact]
    public async Task Write_tool_needs_token_and_own_community()
    {
        var api = factory.CreateAuthorizedClient();
        var pointId = await api.CreatePoint(await api.CreateOperator());
        var args = new Dictionary<string, object?>
        {
            ["pointId"] = pointId, ["status"] = "Limited", ["hasElectricity"] = false,
            ["hasInternet"] = true, ["hasHeating"] = true, ["occupancy"] = 11, ["comment"] = "Через MCP"
        };

        await using var anonymous = await Connect();
        var denied = await anonymous.CallToolAsync("add_status_report", args);
        Assert.True(denied.IsError);
        Assert.Contains("токен", Text(denied));

        var stranger = factory.CreateUser(communityName: "Чужа для MCP");
        await using var other = await Connect(stranger.Token);
        var foreign = await other.CallToolAsync("add_status_report", args);
        Assert.True(foreign.IsError);
        Assert.Contains("іншій громаді", Text(foreign));

        var owner = factory.CreateUser(communityName: "Власна для MCP");
        var ownerApi = factory.CreateClientWithToken(owner.Token);
        var ownPoint = await ownerApi.CreatePoint(await ownerApi.CreateOperator());
        args["pointId"] = ownPoint;
        await using var client = await Connect(owner.Token);
        var added = await client.CallToolAsync("add_status_report", args);
        Assert.NotEqual(true, added.IsError);

        var point = await (await factory.CreateClient().GetAsync($"/api/points/{ownPoint}")).ReadJson();
        Assert.Equal("Limited", point["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Unknown_point_gives_readable_error()
    {
        await using var client = await Connect();

        var result = await client.CallToolAsync("get_point", new Dictionary<string, object?> { ["pointId"] = 987654 });

        Assert.True(result.IsError);
        Assert.Contains("не знайдено", Text(result));
    }
}
