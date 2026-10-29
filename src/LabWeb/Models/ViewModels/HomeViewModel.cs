namespace LabWeb.Models.ViewModels;

public class HomeViewModel
{
    public int PointsTotal { get; set; }
    public int PointsWorking { get; set; }
    public int OperatorsTotal { get; set; }
    public int ReportsToday { get; set; }
    public IReadOnlyList<SkylinePoint> Skyline { get; set; } = [];
    public IReadOnlyList<Point> Points { get; set; } = [];
    public IReadOnlyList<StatusReport> LatestReports { get; set; } = [];

    public int PointsLit => Skyline.Count(p => p.Lit);
}

public record SkylinePoint(int Id, string Name, string Settlement, PointStatus Status, bool Lit);
