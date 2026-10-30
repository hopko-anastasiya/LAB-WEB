using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Auth;

public record ExternalLogin(string? GoogleId, string Email, string Name, string? PictureUrl);

public class UserAccounts(AppDbContext db)
{
    public async Task<AppUser> FindOrCreateAsync(ExternalLogin login, CancellationToken ct = default)
    {
        var email = login.Email.Trim().ToLowerInvariant();
        var user = login.GoogleId is null
            ? await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            : await db.Users.FirstOrDefaultAsync(u => u.GoogleId == login.GoogleId || (u.GoogleId == null && u.Email == email), ct);

        if (user is null)
        {
            user = new AppUser { Email = email };
            db.Users.Add(user);
        }
        user.GoogleId ??= login.GoogleId;
        user.Name = string.IsNullOrWhiteSpace(login.Name) ? email : login.Name.Trim();
        user.PictureUrl = login.PictureUrl;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return user;
    }

    public static ClaimsPrincipal Principal(AppUser user, string scheme = CookieAuthenticationDefaults.AuthenticationScheme) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email)
        ], scheme));

    public async Task<string> IssueApiTokenAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        var token = "nz_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        user.ApiTokenHash = HashToken(token);
        user.ApiTokenCreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task RevokeApiTokenAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.ApiTokenHash = null;
        user.ApiTokenCreatedAt = null;
        await db.SaveChangesAsync(ct);
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static string NewInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Concat(Enumerable.Range(0, 8).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
    }
}
