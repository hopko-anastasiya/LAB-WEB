namespace LabWeb.Models.ViewModels;

public class AnalyticsViewModel
{
    public string CommunityName { get; set; } = string.Empty;
    public IReadOnlyDictionary<PointStatus, int> ByStatus { get; set; } = new Dictionary<PointStatus, int>();
    public IReadOnlyList<AnalyticsDay> Days { get; set; } = [];
    public IReadOnlyList<AnalyticsHour> Hours { get; set; } = [];
    public IReadOnlyList<PointLoad> Load { get; set; } = [];
    public int WithoutElectricity { get; set; }

    public int PointsTotal => ByStatus.Values.Sum();
}

public record AnalyticsDay(DateTime Day, int Reports, int WithElectricity);

public record AnalyticsHour(int Hour, int Reports, int WithElectricity)
{
    public int? Percent => Reports == 0 ? null : (int)Math.Round(100.0 * WithElectricity / Reports);
}

public record PointLoad(int Id, string Name, PointStatus Status, int? Occupancy, int Capacity)
{
    public int? Percent => Occupancy is null || Capacity <= 0 ? null : (int)Math.Round(100.0 * Occupancy.Value / Capacity);
}
