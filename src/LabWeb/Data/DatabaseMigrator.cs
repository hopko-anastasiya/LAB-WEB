using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LabWeb.Data;

public static class DatabaseMigrator
{
    private const string BaselineMigration = "20261030080018_Initial";

    public static async Task MigrateAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Database.CanConnectAsync(ct) && await CreatedWithoutMigrationsAsync(db, ct))
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Settlements" (
                    "Id" integer NOT NULL PRIMARY KEY,
                    "Name" character varying(120) NOT NULL,
                    "Region" character varying(120) NOT NULL,
                    "Latitude" double precision NOT NULL,
                    "Longitude" double precision NOT NULL,
                    "Population" integer NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Settlements_Name" ON "Settlements" ("Name");
                """, ct);
            var history = db.GetService<IHistoryRepository>();
            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
            await db.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(BaselineMigration, ProductInfo.GetVersion())), ct);
        }
        await db.Database.MigrateAsync(ct);
    }

    private static async Task<bool> CreatedWithoutMigrationsAsync(AppDbContext db, CancellationToken ct)
    {
        var tables = await db.Database
            .SqlQueryRaw<string>("""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'public'""")
            .ToListAsync(ct);
        return tables.Contains("Points") && !tables.Contains("__EFMigrationsHistory");
    }
}
