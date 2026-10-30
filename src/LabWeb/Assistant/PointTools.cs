using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabWeb.Auth;
using LabWeb.Data;
using LabWeb.Infrastructure;
using LabWeb.Models;
using LabWeb.Search;
using LabWeb.Services;
using Microsoft.EntityFrameworkCore;

namespace LabWeb.Assistant;

public record PointSummary(
    int Id, string Name, string Settlement, string Address, string Status, string? Schedule,
    IReadOnlyList<string> Services, int Capacity, int? Occupancy, bool? HasElectricity, bool? HasInternet, bool? HasHeating,
    string UpdatedAt, string Url, double? DistanceKm = null);

public record ReportSummary(int Id, string Status, bool HasElectricity, bool HasInternet, bool HasHeating, int Occupancy, string? Comment, string CreatedAt);

public record ServiceSummary(int Id, string Name, string? Description);

public record NearbyResult(string? Center, IReadOnlyList<PointSummary> Points, string? Note = null);

public class PointToolException(string message) : Exception(message);

public class PointTools(AppDbContext db, PointSearchIndex index, PointStatusService statuses)
{
    public const int MaxLimit = 20;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<PointSummary>> SearchAsync(string query, PointStatus? status, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            throw new PointToolException("Запит має містити щонайменше 2 символи.");

        var (matches, _) = index.Search(query, status, 0, ClampLimit(limit));
        var ids = matches.Select(m => m.PointId).ToList();
        var points = await SummariesAsync(db.Points.Where(p => ids.Contains(p.Id)), ct);
        return ids.Select(id => points.FirstOrDefault(p => p.Id == id)).OfType<PointSummary>().ToList();
    }

    public async Task<NearbyResult> FindNearbyAsync(
        string? settlement, double? latitude, double? longitude, IReadOnlyList<string>? services,
        bool onlyOpen, double radiusKm, int limit, CancellationToken ct)
    {
        var center = settlement?.Trim();
        if (latitude is null || longitude is null)
        {
            if (string.IsNullOrEmpty(center))
                throw new PointToolException("Вкажіть населений пункт або координати.");
            var found = await db.Settlements.AsNoTracking()
                .Where(s => EF.Functions.ILike(s.Name, center))
                .OrderByDescending(s => s.Population)
                .FirstOrDefaultAsync(ct);
            if (found is null)
                return new NearbyResult(center, [], $"Населений пункт «{center}» не знайдено в довіднику.");
            (latitude, longitude, center) = (found.Latitude, found.Longitude, $"{found.Name}, {found.Region}");
        }

        var query = db.Points.AsQueryable();
        if (onlyOpen)
            query = query.Where(p => p.Status != PointStatus.Closed);
        foreach (var service in (services ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => $"%{s.Trim()}%"))
            query = query.Where(p => p.Services.Any(s => EF.Functions.ILike(s.Name, service)));

        var radius = Math.Clamp(radiusKm <= 0 ? 30 : radiusKm, 1, 1000);
        var points = (await SummariesWithCoordinatesAsync(query, ct))
            .Select(p => p.Summary with { DistanceKm = Math.Round(Distance(latitude.Value, longitude.Value, p.Latitude, p.Longitude), 1) })
            .Where(p => p.DistanceKm <= radius)
            .OrderBy(p => p.Status == PointStatus.Working.DisplayName() ? 0 : 1)
            .ThenBy(p => p.DistanceKm)
            .Take(ClampLimit(limit))
            .ToList();
        return new NearbyResult(center, points);
    }

    public async Task<PointSummary> GetPointAsync(int id, CancellationToken ct) =>
        (await SummariesAsync(db.Points.Where(p => p.Id == id), ct)).FirstOrDefault()
        ?? throw new PointToolException($"Пункт {id} не знайдено.");

    public async Task<IReadOnlyList<ReportSummary>> GetReportsAsync(int pointId, int limit, CancellationToken ct)
    {
        if (!await db.Points.AnyAsync(p => p.Id == pointId, ct))
            throw new PointToolException($"Пункт {pointId} не знайдено.");

        var reports = await db.StatusReports.AsNoTracking()
            .Where(r => r.PointId == pointId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(ClampLimit(limit))
            .ToListAsync(ct);
        return reports.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ServiceSummary(s.Id, s.Name, s.Description))
            .ToListAsync(ct);

    public async Task<ReportSummary> AddReportAsync(
        CurrentUser user, int pointId, PointStatus status, bool hasElectricity, bool hasInternet, bool hasHeating,
        int occupancy, string? comment, CancellationToken ct)
    {
        if (occupancy is < 0 or > 10000)
            throw new PointToolException("Кількість людей має бути від 0 до 10000.");
        if (comment?.Length > 1000)
            throw new PointToolException("Коментар задовгий: максимум 1000 символів.");

        var point = await db.Points.Include(p => p.Operator).FirstOrDefaultAsync(p => p.Id == pointId, ct)
                    ?? throw new PointToolException($"Пункт {pointId} не знайдено.");
        if (!user.CanEdit(point.Operator?.CommunityId))
            throw new PointToolException("Пункт належить іншій громаді.");

        var report = new StatusReport
        {
            PointId = point.Id, Status = status, HasElectricity = hasElectricity, HasInternet = hasInternet,
            HasHeating = hasHeating, Occupancy = occupancy, Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            AuthorName = user.User!.Name, CreatedAt = DateTime.UtcNow
        };
        db.StatusReports.Add(report);
        statuses.ApplyNewReport(point, report);
        await db.SaveChangesAsync(ct);
        return ToSummary(report);
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit <= 0 ? 5 : limit, 1, MaxLimit);

    private static ReportSummary ToSummary(StatusReport r) => new(
        r.Id, r.Status.DisplayName(), r.HasElectricity, r.HasInternet, r.HasHeating, r.Occupancy, r.Comment, r.CreatedAt.ToLocalString());

    private async Task<List<PointSummary>> SummariesAsync(IQueryable<Point> query, CancellationToken ct) =>
        (await SummariesWithCoordinatesAsync(query, ct)).Select(p => p.Summary).ToList();

    private static async Task<List<(PointSummary Summary, double Latitude, double Longitude)>> SummariesWithCoordinatesAsync(IQueryable<Point> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .Select(p => new
            {
                Point = p,
                Services = p.Services.OrderBy(s => s.Name).Select(s => s.Name).ToList(),
                Last = p.Reports.OrderByDescending(r => r.CreatedAt).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => (
            new PointSummary(
                r.Point.Id, r.Point.Name, r.Point.Settlement, r.Point.Address, r.Point.Status.DisplayName(), r.Point.Schedule,
                r.Services, r.Point.Capacity, r.Last?.Occupancy, r.Last?.HasElectricity, r.Last?.HasInternet, r.Last?.HasHeating,
                r.Point.UpdatedAt.ToLocalString(), $"/punkt/{r.Point.Id}"),
            r.Point.Latitude, r.Point.Longitude)).ToList();
    }

    private static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthKm = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * earthKm * Math.Asin(Math.Sqrt(a));
    }
}
