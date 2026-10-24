namespace LabWeb.Models;

public class PointSubscription
{
    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public int PointId { get; set; }
    public Point? Point { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    public Guid EventId { get; set; }

    public int? PointId { get; set; }
    public Point? Point { get; set; }

    public string PointName { get; set; } = string.Empty;

    public PointStatus OldStatus { get; set; }
    public PointStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
