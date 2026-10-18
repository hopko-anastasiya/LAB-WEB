using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;

namespace LabWeb.Hubs;

public record BoardItemDto(int Id, BoardItemKind Kind, string Color, int Width, double[][] Points, string? Text, string AuthorName, int? AuthorId, string? ClientId);

public record BoardState(IReadOnlyList<BoardItemDto> Items, int UserId, bool IsAdmin);

[Authorize]
public class BoardHub(AppDbContext db, IFeatureManager features) : Hub
{
    public const int MaxStrokePoints = 1500;
    public const int MaxProgressPoints = 200;
    public const int MaxNoteLength = 200;
    public static readonly string[] Palette = ["#F5B841", "#E8833A", "#6F8FD6", "#E8ECF4"];
    private static readonly int[] Widths = [3, 6, 12];

    private static readonly ConcurrentDictionary<string, (int CommunityId, string Name)> Online = new();

    private static string Group(int communityId) => $"board-{communityId}";

    public override async Task OnConnectedAsync()
    {
        var user = await FindMemberAsync();
        if (user is null)
        {
            Context.Abort();
            return;
        }

        Online[Context.ConnectionId] = (user.CommunityId!.Value, user.Name);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(user.CommunityId.Value));
        await SendPresenceAsync(user.CommunityId.Value);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Online.TryRemove(Context.ConnectionId, out var entry))
            await SendPresenceAsync(entry.CommunityId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<BoardState> Load()
    {
        var user = await RequireMemberAsync();
        var items = await db.BoardItems.AsNoTracking()
            .Where(i => i.CommunityId == user.CommunityId)
            .OrderBy(i => i.Id)
            .ToListAsync();
        return new BoardState(items.Select(i => ToDto(i, null)).ToList(), user.Id, user.Role == CommunityRole.Admin);
    }

    public async Task DrawProgress(string clientId, string color, int width, double[][] points)
    {
        if (!Online.TryGetValue(Context.ConnectionId, out var entry)) return;
        if (points.Length > MaxProgressPoints || clientId.Length > 40) throw new HubException("Забагато точок.");
        ValidateStyle(color, width);
        ValidatePoints(points);
        await Clients.OthersInGroup(Group(entry.CommunityId)).SendAsync("drawProgress", clientId, color, width, points);
    }

    public async Task<int> AddStroke(string clientId, string color, int width, double[][] points)
    {
        var user = await RequireMemberAsync();
        ValidateStyle(color, width);
        if (points.Length is < 2 or > MaxStrokePoints) throw new HubException("Лінія має містити від 2 до 1500 точок.");
        ValidatePoints(points);
        return await SaveAsync(user, new BoardItem { Kind = BoardItemKind.Stroke, Color = color, Width = width, Points = JsonSerializer.Serialize(points) }, clientId);
    }

    public async Task<int> AddNote(string color, double latitude, double longitude, string text)
    {
        var user = await RequireMemberAsync();
        ValidateStyle(color, Widths[0]);
        ValidatePoints([[latitude, longitude]]);
        text = text?.Trim() ?? "";
        if (text.Length is 0 or > MaxNoteLength) throw new HubException("Примітка має містити від 1 до 200 символів.");
        return await SaveAsync(user, new BoardItem { Kind = BoardItemKind.Note, Color = color, Width = Widths[0], Text = text, Points = JsonSerializer.Serialize(new[] { new[] { latitude, longitude } }) }, null);
    }

    public async Task DeleteItem(int id)
    {
        var user = await RequireMemberAsync();
        var item = await db.BoardItems.FirstOrDefaultAsync(i => i.Id == id && i.CommunityId == user.CommunityId);
        if (item is null) return;
        if (item.AuthorId != user.Id && user.Role != CommunityRole.Admin)
            throw new HubException("Видаляти чужі позначки може лише адміністратор громади.");

        db.BoardItems.Remove(item);
        await db.SaveChangesAsync();
        await Clients.Group(Group(user.CommunityId!.Value)).SendAsync("itemRemoved", id);
    }

    public async Task Clear()
    {
        var user = await RequireMemberAsync();
        if (user.Role != CommunityRole.Admin)
            throw new HubException("Очистити дошку може лише адміністратор громади.");

        await db.BoardItems.Where(i => i.CommunityId == user.CommunityId).ExecuteDeleteAsync();
        await Clients.Group(Group(user.CommunityId!.Value)).SendAsync("cleared");
    }

    private async Task<int> SaveAsync(AppUser user, BoardItem item, string? clientId)
    {
        item.CommunityId = user.CommunityId!.Value;
        item.AuthorId = user.Id;
        item.AuthorName = user.Name;
        db.BoardItems.Add(item);
        await db.SaveChangesAsync();
        await Clients.Group(Group(item.CommunityId)).SendAsync("itemAdded", ToDto(item, clientId));
        return item.Id;
    }

    private async Task<AppUser> RequireMemberAsync() =>
        await FindMemberAsync() ?? throw new HubException("Дошка доступна учасникам громад із планом Premium.");

    private async Task<AppUser?> FindMemberAsync()
    {
        if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var user = await db.Users.AsNoTracking().Include(u => u.Community).FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Community is null) return null;

        var enabled = await features.IsEnabledAsync(Features.SharedBoard, user.Community.Plan);
        if (!enabled) return null;

        if (Online.TryGetValue(Context.ConnectionId, out var entry) && entry.CommunityId != user.CommunityId)
            return null;
        return user;
    }

    private async Task SendPresenceAsync(int communityId)
    {
        var names = Online.Values.Where(v => v.CommunityId == communityId).Select(v => v.Name).Distinct().OrderBy(n => n).ToList();
        await Clients.Group(Group(communityId)).SendAsync("presence", names);
    }

    private static void ValidateStyle(string color, int width)
    {
        if (!Palette.Contains(color) || !Widths.Contains(width))
            throw new HubException("Такого кольору чи товщини немає.");
    }

    private static void ValidatePoints(double[][] points)
    {
        foreach (var p in points)
        {
            if (p.Length != 2 || !double.IsFinite(p[0]) || !double.IsFinite(p[1]) || Math.Abs(p[0]) > 90 || Math.Abs(p[1]) > 180)
                throw new HubException("Некоректні координати.");
        }
    }

    private static BoardItemDto ToDto(BoardItem i, string? clientId) => new(
        i.Id, i.Kind, i.Color, i.Width, JsonSerializer.Deserialize<double[][]>(i.Points) ?? [], i.Text, i.AuthorName, i.AuthorId, clientId);
}
