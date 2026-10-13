using System.Net;
using System.Text.Json;
using LabWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class LiveUpdatesTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthorizedClient();
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private HubConnection Connect() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/points"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

    private static Task<JsonElement> NextChange(HubConnection connection, int pointId)
    {
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("pointChanged", p =>
        {
            if (p.GetProperty("id").GetInt32() == pointId)
                tcs.TrySetResult(p);
        });
        return tcs.Task;
    }

    [Fact]
    public async Task Every_client_gets_new_status_after_report()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        await using var first = Connect();
        await using var second = Connect();
        var firstChange = NextChange(first, pointId);
        var secondChange = NextChange(second, pointId);
        await first.StartAsync();
        await second.StartAsync();

        await (await _client.PostJson("/api/reports", new { pointId, status = "Closed", occupancy = 4 }))
            .ExpectJson(HttpStatusCode.Created);

        var a = await firstChange.WaitAsync(Wait);
        var b = await secondChange.WaitAsync(Wait);
        Assert.Equal("Closed", a.GetProperty("status").GetString());
        Assert.Equal(4, b.GetProperty("occupancy").GetInt32());
    }

    [Fact]
    public async Task Deleted_point_is_announced()
    {
        var pointId = await _client.CreatePoint(await _client.CreateOperator());
        await using var connection = Connect();
        var removed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<int>("pointRemoved", id =>
        {
            if (id == pointId) removed.TrySetResult(id);
        });
        await connection.StartAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/points/{pointId}")).StatusCode);

        Assert.Equal(pointId, await removed.Task.WaitAsync(Wait));
    }
}
