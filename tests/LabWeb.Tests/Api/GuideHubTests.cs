using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabWeb.Hubs;
using LabWeb.Models;
using LabWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class GuideHubTests(ApiFactory factory)
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private async Task<HubConnection> Join(TestUser user)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/guide"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(user.Token);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private TestUser NewAdmin() => factory.CreateUser(communityName: $"Док {Guid.NewGuid():N}"[..16]);

    private static GuideBlockDto Block(string text = "Текст", double position = 1, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), GuideBlockType.Paragraph, text, position);

    private static Task<JsonElement> NextChange(HubConnection connection)
    {
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement, string>("blockChanged", (b, _) => tcs.TrySetResult(b));
        return tcs.Task;
    }

    [Fact]
    public async Task Edit_reaches_other_device_and_is_stored()
    {
        var admin = NewAdmin();
        await using var first = await Join(admin);
        await using var second = await Join(factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId));
        var received = NextChange(second);
        var block = Block("Де лежить ключ від генератора");

        await first.InvokeAsync("Upsert", block);

        Assert.Equal(block.Text, (await received.WaitAsync(Wait)).GetProperty("text").GetString());
        var loaded = await second.InvokeAsync<List<GuideBlockDto>>("Load");
        Assert.Equal(block.Id, Assert.Single(loaded).Id);
    }

    [Fact]
    public async Task Two_people_opening_empty_document_get_one_starting_line()
    {
        var admin = NewAdmin();
        await using var first = await Join(admin);
        await using var second = await Join(factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId));

        var opened = await Task.WhenAll(
            first.InvokeAsync<List<GuideBlockDto>>("Open"),
            second.InvokeAsync<List<GuideBlockDto>>("Open"));

        var line = Assert.Single(opened[0]);
        Assert.Equal(line.Id, Assert.Single(opened[1]).Id);
        Assert.Equal(GuideBlockType.Heading, line.Type);

        await first.InvokeAsync("Upsert", line with { Text = "Назва" });
        Assert.Equal("Назва", Assert.Single(await second.InvokeAsync<List<GuideBlockDto>>("Open")).Text);
    }

    [Fact]
    public async Task Order_comes_from_positions_and_moves_are_saved()
    {
        await using var connection = await Join(NewAdmin());
        var a = Block("А", 1);
        var b = Block("Б", 2);
        await connection.InvokeAsync("Upsert", a);
        await connection.InvokeAsync("Upsert", b);

        await connection.InvokeAsync("Upsert", a with { Position = 3 });

        var loaded = await connection.InvokeAsync<List<GuideBlockDto>>("Load");
        Assert.Equal(["Б", "А"], loaded.Select(x => x.Text));
    }

    [Fact]
    public async Task Delete_removes_block_for_everyone()
    {
        var admin = NewAdmin();
        await using var first = await Join(admin);
        await using var second = await Join(factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId));
        var block = Block();
        await first.InvokeAsync("Upsert", block);
        var removed = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.On<Guid, string>("blockRemoved", (id, _) => removed.TrySetResult(id));

        await first.InvokeAsync("Delete", block.Id);

        Assert.Equal(block.Id, await removed.Task.WaitAsync(Wait));
        Assert.Empty(await second.InvokeAsync<List<GuideBlockDto>>("Load"));
    }

    [Fact]
    public async Task Other_community_cannot_read_change_or_delete()
    {
        await using var owner = await Join(NewAdmin());
        await using var stranger = await Join(NewAdmin());
        var block = Block("Таємниця");
        await owner.InvokeAsync("Upsert", block);

        Assert.Empty(await stranger.InvokeAsync<List<GuideBlockDto>>("Load"));
        await Assert.ThrowsAsync<HubException>(() => stranger.InvokeAsync("Upsert", block with { Text = "Зламано" }));
        await stranger.InvokeAsync("Delete", block.Id);

        var kept = Assert.Single(await owner.InvokeAsync<List<GuideBlockDto>>("Load"));
        Assert.Equal("Таємниця", kept.Text);
    }

    [Fact]
    public async Task Too_long_text_is_rejected_and_editing_is_announced()
    {
        var admin = NewAdmin();
        await using var first = await Join(admin);
        await using var second = await Join(factory.CreateUser(role: CommunityRole.Member, communityId: admin.CommunityId));
        await Assert.ThrowsAsync<HubException>(() => first.InvokeAsync("Upsert", Block(new string('а', GuideHub.MaxTextLength + 1))));

        var editing = new TaskCompletionSource<Guid?>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.On<string, Guid?, string?>("editing", (_, blockId, _) => editing.TrySetResult(blockId));
        var id = Guid.NewGuid();
        await first.InvokeAsync("Editing", id);

        Assert.Equal(id, await editing.Task.WaitAsync(Wait));
    }

    [Fact]
    public async Task Page_is_for_community_members_only()
    {
        var anonymous = new BrowserSession(factory);
        Assert.StartsWith("/Account/Login", (await anonymous.Client.GetAsync("/Guide")).Headers.Location!.PathAndQuery);

        var noCommunity = new BrowserSession(factory);
        await noCommunity.SignIn(factory.CreateUser().Email);
        Assert.StartsWith("/Account/Denied", (await noCommunity.Client.GetAsync("/Guide")).Headers.Location!.PathAndQuery);

        var member = new BrowserSession(factory);
        await member.SignIn(NewAdmin().Email);
        Assert.Equal(HttpStatusCode.OK, (await member.Client.GetAsync("/Guide")).StatusCode);
    }
}
