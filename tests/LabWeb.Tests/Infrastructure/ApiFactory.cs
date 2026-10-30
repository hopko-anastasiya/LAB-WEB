using LabWeb.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LabWeb.Tests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDatabase = "labweb_tests";

    public string ConnectionString { get; } = BuildConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString
            }));
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

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
