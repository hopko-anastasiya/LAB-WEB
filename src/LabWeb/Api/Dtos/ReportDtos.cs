using System.ComponentModel.DataAnnotations;
using LabWeb.Models;

namespace LabWeb.Api.Dtos;

public record ReportDto(
    int Id,
    int PointId,
    string? PointName,
    PointStatus Status,
    bool HasElectricity,
    bool HasInternet,
    bool HasHeating,
    int Occupancy,
    string? Comment,
    string? PhotoUrl,
    string? AuthorName,
    DateTime CreatedAt);

public class ReportInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть пункт")]
    public int PointId { get; set; }

    public PointStatus Status { get; set; } = PointStatus.Working;

    public bool HasElectricity { get; set; }
    public bool HasInternet { get; set; }
    public bool HasHeating { get; set; }

    [Range(0, 10000, ErrorMessage = "Значення має бути від {1} до {2}")]
    public int Occupancy { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    [StringLength(120)]
    public string? AuthorName { get; set; }
}
