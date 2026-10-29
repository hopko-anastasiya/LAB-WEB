namespace LabWeb.Models.ViewModels;

public class HomeViewModel
{
    public int PointsTotal { get; set; }
    public int PointsWorking { get; set; }
    public int OperatorsTotal { get; set; }
    public int ReportsToday { get; set; }
    public IReadOnlyList<Point> Points { get; set; } = [];
    public IReadOnlyList<StatusReport> LatestReports { get; set; } = [];
}
