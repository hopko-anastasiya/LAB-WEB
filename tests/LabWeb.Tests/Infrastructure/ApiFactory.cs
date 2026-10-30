using System.Net.Http.Headers;
using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Models;
using Microsoft.AspNetCore.Hosting;
using LabWeb.Telegram;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LabWeb.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDatabase = "labweb_tests";

    public const string TelegramSecret = "test-secret";

    public string ConnectionString { get; } = BuildConnectionString();

    public FakeTelegramApi Telegram { get; } = new();

    public const string SystemAdminEmail = "admin@tests.local";

    private readonly Lazy<string> _adminToken;

    public ApiFactory() =>
        _adminToken = new Lazy<string>(() => CreateUser(SystemAdminEmail, "Тестова громада", Plan.Premium).Token);

    public HttpClient CreateAuthorizedClient() => CreateClientWithToken(_adminToken.Value);

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public TestUser CreateUser(string? email = null, string? communityName = null, Plan plan = Plan.Free,
        CommunityRole role = CommunityRole.Admin, int? communityId = null)
    {
        _ = Server;
        using var db = CreateDbContext();
        var token = "test_" + Guid.NewGuid().ToString("N");
        var user = new AppUser
        {
            Email = email ?? $"user-{Guid.NewGuid():N}@tests.local",
            Name = "Тестовий користувач",
            Role = role,
            ApiTokenHash = UserAccounts.HashToken(token),
            ApiTokenCreatedAt = DateTime.UtcNow
        };
        if (communityId is not null)
            user.CommunityId = communityId;
        else if (communityName is not null)
            user.Community = new Community { Name = communityName, Plan = plan, InviteCode = UserAccounts.NewInviteCode() };
        db.Users.Add(user);
        db.SaveChanges();
        return new TestUser(user.Id, user.Email, user.CommunityId, token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Telegram:BotToken"] = "test-token",
                ["Telegram:WebhookSecret"] = TelegramSecret,
                ["Auth:SystemAdmins:0"] = SystemAdminEmail
            }));
        builder.UseSetting("Auth:Google:ClientId", "test-google-client");
        builder.UseSetting("Auth:Google:ClientSecret", "test-google-secret");
        builder.ConfigureTestServices(services => services.AddSingleton<ITelegramApi>(Telegram));
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    private static string BuildConnectionString()
    {
        var appDir = FindAppDirectory();
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(appDir, "appsettings.json"), optional: false)
            .AddJsonFile(Path.Combine(appDir, "appsettings.Local.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        var csb = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Default"))
        {
            Database = TestDatabase,
            Pooling = false
        };
        return csb.ConnectionString;
    }

    private static string FindAppDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LabWeb.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Не знайдено LabWeb.sln поруч із тестами");
        return Path.Combine(dir.FullName, "src", "LabWeb");
    }
}

public record TestUser(int Id, string Email, int? CommunityId, string Token);

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
