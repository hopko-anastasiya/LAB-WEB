using System.ComponentModel.DataAnnotations;
using LabWeb.Models;

namespace LabWeb.Api.Dtos;

public record PointDto(
    int Id,
    string Name,
    string Address,
    string Settlement,
    double Latitude,
    double Longitude,
    string? Description,
    string? Schedule,
    int Capacity,
    PointStatus Status,
    string? PhotoUrl,
    OperatorRef? Operator,
    IReadOnlyList<ServiceRef> Services,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public class PointInput
{
    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(120)]
    public string Settlement { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Значення має бути від {1} до {2}")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Значення має бути від {1} до {2}")]
    public double Longitude { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? Schedule { get; set; }

    [Range(0, 10000, ErrorMessage = "Значення має бути від {1} до {2}")]
    public int Capacity { get; set; }

    public PointStatus Status { get; set; } = PointStatus.Working;

    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть оператора")]
    public int OperatorId { get; set; }

    public List<int> ServiceIds { get; set; } = [];
}
