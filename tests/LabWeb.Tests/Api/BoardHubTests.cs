using System.Net;
using System.Text.Json;
using LabWeb.Models;
using LabWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class BoardHubTests(ApiFactory factory)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private const string Yellow = "#F5B841";
    private static readonly double[][] Line = [[50.45, 30.52], [50.46, 30.53]];

    private HubConnection Connect(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/board"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (token is not null)
                    o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private async Task<(HubConnection Connection, TestUser User)> Join(TestUser user)
    {
        var connection = Connect(user.Token);
        await connection.StartAsync();
        return (connection, user);
    }

    private TestUser NewAdmin(Plan plan = Plan.Premium) =>
        factory.CreateUser(communityName: $"Дошка {Guid.NewGuid():N}"[..20], plan: plan);

    private TestUser NewMember(TestUser admin) =>
        factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId);

    private static Task<JsonElement> NextItem(HubConnection connection)
    {
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("itemAdded", i => tcs.TrySetResult(i));
        return tcs.Task;
    }

    private static async Task<JsonElement> Load(HubConnection connection) =>
        (await connection.InvokeAsync<JsonElement>("Load")).GetProperty("items");

    [Fact]
    public async Task Stroke_reaches_community_members_and_stays_on_the_board()
    {
        var admin = NewAdmin();
        var (first, _) = await Join(admin);
        var (second, _) = await Join(NewMember(admin));
        await using var _1 = first;
        await using var _2 = second;
        var received = NextItem(second);

        var id = await first.InvokeAsync<int>("AddStroke", "tmp-1", Yellow, 3, Line);

        var item = await received.WaitAsync(Wait);
        Assert.Equal(id, item.GetProperty("id").GetInt32());
        Assert.Equal("tmp-1", item.GetProperty("clientId").GetString());
        Assert.Equal(30.53, item.GetProperty("points")[1][1].GetDouble());

        var (late, _) = await Join(NewMember(admin));
        await using var _3 = late;
        Assert.Contains((await Load(late)).EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Other_community_sees_nothing()
    {
        var (mine, _) = await Join(NewAdmin());
        var (other, _) = await Join(NewAdmin());
        await using var _1 = mine;
        await using var _2 = other;
        var leaked = NextItem(other);

        var id = await mine.InvokeAsync<int>("AddStroke", "tmp", Yellow, 3, Line);
        await Task.WhenAny(leaked, Task.Delay(500));

        Assert.False(leaked.IsCompleted);
        Assert.DoesNotContain((await Load(other)).EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);
        await other.InvokeAsync("DeleteItem", id);
        Assert.Contains((await Load(mine)).EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Drawing_in_progress_is_sent_to_others()
    {
        var admin = NewAdmin();
        var (first, _) = await Join(admin);
        var (second, _) = await Join(NewMember(admin));
        await using var _1 = first;
        await using var _2 = second;
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.On<string, string, int, double[][]>("drawProgress", (clientId, _, _, _) => tcs.TrySetResult(clientId));

        await first.InvokeAsync("DrawProgress", "draft-1", Yellow, 3, Line);

        Assert.Equal("draft-1", await tcs.Task.WaitAsync(Wait));
    }

    [Fact]
    public async Task Free_plan_and_anonymous_have_no_board()
    {
        var free = Connect(NewAdmin(Plan.Free).Token);
        await using var _1 = free;
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await free.StartAsync();
            await free.InvokeAsync<JsonElement>("Load");
        });

        var anonymous = Connect(null);
        await using var _2 = anonymous;
        var error = await Assert.ThrowsAnyAsync<HttpRequestException>(() => anonymous.StartAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Only_author_or_admin_deletes_and_only_admin_clears()
    {
        var admin = NewAdmin();
        var (adminConn, _) = await Join(admin);
        var (member, _) = await Join(NewMember(admin));
        await using var _1 = adminConn;
        await using var _2 = member;

        var adminItem = await adminConn.InvokeAsync<int>("AddStroke", "a", Yellow, 3, Line);
        var memberItem = await member.InvokeAsync<int>("AddNote", Yellow, 50.4, 30.5, "Тут пальне");

        await Assert.ThrowsAsync<HubException>(() => member.InvokeAsync("DeleteItem", adminItem));
        await Assert.ThrowsAsync<HubException>(() => member.InvokeAsync("Clear"));
        await member.InvokeAsync("DeleteItem", memberItem);
        Assert.Single((await Load(adminConn)).EnumerateArray());

        await adminConn.InvokeAsync("Clear");
        Assert.Empty((await Load(adminConn)).EnumerateArray());
    }

    [Fact]
    public async Task Bad_input_is_rejected()
    {
        var (connection, _) = await Join(NewAdmin());
        await using var _1 = connection;

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddStroke", "c", "red;", 3, Line));
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddStroke", "w", Yellow, 99, Line));
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddStroke", "s", Yellow, 3, new[] { new[] { 1.0, 1.0 } }));
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddStroke", "r", Yellow, 3, new[] { new[] { 500.0, 1.0 }, new[] { 1.0, 1.0 } }));
        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddNote", Yellow, 50.0, 30.0, "   "));
    }

    [Fact]
    public async Task Member_removed_from_community_loses_write_access()
    {
        var admin = NewAdmin();
        var member = NewMember(admin);
        var (connection, _) = await Join(member);
        await using var _1 = connection;
        await using (var db = factory.CreateDbContext())
        {
            db.Users.Single(u => u.Id == member.Id).CommunityId = null;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<int>("AddStroke", "x", Yellow, 3, Line));
    }

    [Fact]
    public async Task Board_page_is_premium_only()
    {
        var free = new BrowserSession(factory);
        await free.SignIn(NewAdmin(Plan.Free).Email);
        var premium = new BrowserSession(factory);
        await premium.SignIn(NewAdmin().Email);

        Assert.Equal("/Community/Premium", (await free.Client.GetAsync("/Board")).Headers.Location!.OriginalString);
        Assert.StartsWith("/Account/Login", (await new BrowserSession(factory).Client.GetAsync("/Board")).Headers.Location!.PathAndQuery);
        Assert.Contains("Штабна мапа", await premium.Client.GetStringAsync("/Board"));
    }
}
