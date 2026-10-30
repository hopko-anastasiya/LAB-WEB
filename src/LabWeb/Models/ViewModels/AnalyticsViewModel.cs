namespace LabWeb.Models.ViewModels;

public class AnalyticsViewModel
{
    public string CommunityName { get; set; } = string.Empty;
    public IReadOnlyDictionary<PointStatus, int> ByStatus { get; set; } = new Dictionary<PointStatus, int>();
    public IReadOnlyList<AnalyticsDay> Days { get; set; } = [];
    public IReadOnlyList<PointLoad> Load { get; set; } = [];
    public int WithoutElectricity { get; set; }

    public int PointsTotal => ByStatus.Values.Sum();
    public int MaxReportsPerDay => Math.Max(1, Days.Count == 0 ? 0 : Days.Max(d => d.Reports));
}

public record AnalyticsDay(DateTime Day, int Reports, int WithElectricity);

public record PointLoad(int Id, string Name, PointStatus Status, int? Occupancy, int Capacity)
{
    public int? Percent => Occupancy is null || Capacity <= 0 ? null : (int)Math.Round(100.0 * Occupancy.Value / Capacity);
}
