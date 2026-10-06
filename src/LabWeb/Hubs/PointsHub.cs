using Microsoft.AspNetCore.SignalR;

namespace LabWeb.Hubs;

public class PointsHub : Hub
{
    private static int _viewers;

    public override async Task OnConnectedAsync()
    {
        var count = Interlocked.Increment(ref _viewers);
        await Clients.All.SendAsync("viewers", count);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var count = Interlocked.Decrement(ref _viewers);
        await Clients.All.SendAsync("viewers", count);
        await base.OnDisconnectedAsync(exception);
    }
}
