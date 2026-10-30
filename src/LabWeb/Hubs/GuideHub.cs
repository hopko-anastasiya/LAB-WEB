using System.Collections.Concurrent;
using System.Security.Claims;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Hubs;

public record GuideBlockDto(Guid Id, GuideBlockType Type, string Text, double Position);

[Authorize]
public class GuideHub(AppDbContext db) : Hub
{
    public const int MaxTextLength = 5000;
    public const int MaxBlocks = 500;

    private static readonly ConcurrentDictionary<string, (int CommunityId, string Name)> Online = new();

    private static string Group(int communityId) => $"guide-{communityId}";

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
        {
            await Clients.OthersInGroup(Group(entry.CommunityId)).SendAsync("editing", Context.ConnectionId, null, null);
            await SendPresenceAsync(entry.CommunityId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<IReadOnlyList<GuideBlockDto>> Load()
    {
        var user = await RequireMemberAsync();
        return await db.GuideBlocks.AsNoTracking()
            .Where(b => b.CommunityId == user.CommunityId)
            .OrderBy(b => b.Position)
            .Select(b => new GuideBlockDto(b.Id, b.Type, b.Text, b.Position))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<GuideBlockDto>> Open()
    {
        var user = await RequireMemberAsync();
        var communityId = user.CommunityId!.Value;
        var starterId = new Guid(communityId, 0, 0, new byte[8]);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GuideBlocks" ("Id", "CommunityId", "Type", "Text", "Position", "UpdatedAt")
            SELECT {starterId}, {communityId}, {nameof(GuideBlockType.Heading)}, '', 1, {DateTime.UtcNow}
            WHERE NOT EXISTS (SELECT 1 FROM "GuideBlocks" WHERE "CommunityId" = {communityId})
            ON CONFLICT ("Id") DO NOTHING
            """);
        return await Load();
    }

    public async Task Upsert(GuideBlockDto block)
    {
        var user = await RequireMemberAsync();
        Validate(block);

        var existing = await db.GuideBlocks.FirstOrDefaultAsync(b => b.Id == block.Id);
        if (existing is null)
        {
            if (await db.GuideBlocks.CountAsync(b => b.CommunityId == user.CommunityId) >= MaxBlocks)
                throw new HubException($"У документі може бути щонайбільше {MaxBlocks} блоків.");
            existing = new GuideBlock { Id = block.Id, CommunityId = user.CommunityId!.Value };
            db.GuideBlocks.Add(existing);
        }
        else if (existing.CommunityId != user.CommunityId)
        {
            throw new HubException("Цей блок належить іншій громаді.");
        }

        existing.Type = block.Type;
        existing.Text = block.Text;
        existing.Position = block.Position;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await Clients.OthersInGroup(Group(user.CommunityId!.Value)).SendAsync("blockChanged", block, user.Name);
    }

    public async Task Delete(Guid id)
    {
        var user = await RequireMemberAsync();
        await db.GuideBlocks.Where(b => b.Id == id && b.CommunityId == user.CommunityId).ExecuteDeleteAsync();
        await Clients.OthersInGroup(Group(user.CommunityId!.Value)).SendAsync("blockRemoved", id, user.Name);
    }

    public async Task Editing(Guid? blockId)
    {
        if (!Online.TryGetValue(Context.ConnectionId, out var entry)) return;
        await Clients.OthersInGroup(Group(entry.CommunityId)).SendAsync("editing", Context.ConnectionId, blockId, entry.Name);
    }

    private static void Validate(GuideBlockDto block)
    {
        if (block.Id == Guid.Empty || !Enum.IsDefined(block.Type) || !double.IsFinite(block.Position))
            throw new HubException("Некоректний блок.");
        if (block.Text is null || block.Text.Length > MaxTextLength)
            throw new HubException($"Блок може містити щонайбільше {MaxTextLength} символів.");
    }

    private async Task<AppUser> RequireMemberAsync() =>
        await FindMemberAsync() ?? throw new HubException("Документ доступний лише учасникам громади.");

    private async Task<AppUser?> FindMemberAsync()
    {
        if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user?.CommunityId is null) return null;
        if (Online.TryGetValue(Context.ConnectionId, out var entry) && entry.CommunityId != user.CommunityId) return null;
        return user;
    }

    private async Task SendPresenceAsync(int communityId)
    {
        var names = Online.Values.Where(v => v.CommunityId == communityId).Select(v => v.Name).Distinct().OrderBy(n => n).ToList();
        await Clients.Group(Group(communityId)).SendAsync("presence", names);
    }
}
