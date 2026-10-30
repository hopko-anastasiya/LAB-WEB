using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LabWeb.Data;

public static class SettlementImporter
{
    private const string DataFile = "Data/settlements-ua.tsv.gz";

    public static async Task ImportAsync(AppDbContext db, string contentRoot, CancellationToken ct = default)
    {
        if (await db.Settlements.AnyAsync(ct)) return;

        var path = Path.Combine(contentRoot, DataFile);
        if (!File.Exists(path)) return;

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var importer = await connection.BeginBinaryImportAsync(
                """COPY "Settlements" ("Id", "Name", "Region", "Latitude", "Longitude", "Population") FROM STDIN (FORMAT BINARY)""", ct);

            await using var file = File.OpenRead(path);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                var c = line.Split('\t');
                await importer.StartRowAsync(ct);
                await importer.WriteAsync(int.Parse(c[0], CultureInfo.InvariantCulture), NpgsqlDbType.Integer, ct);
                await importer.WriteAsync(c[1], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(c[2], NpgsqlDbType.Varchar, ct);
                await importer.WriteAsync(double.Parse(c[3], CultureInfo.InvariantCulture), NpgsqlDbType.Double, ct);
                await importer.WriteAsync(double.Parse(c[4], CultureInfo.InvariantCulture), NpgsqlDbType.Double, ct);
                await importer.WriteAsync(int.Parse(c[5], CultureInfo.InvariantCulture), NpgsqlDbType.Integer, ct);
            }
            await importer.CompleteAsync(ct);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
