using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public class StatusReport
{
    public int Id { get; set; }

    [Display(Name = "Пункт")]
    public int PointId { get; set; }
    public Point? Point { get; set; }

    [Display(Name = "Статус")]
    public PointStatus Status { get; set; } = PointStatus.Working;

    [Display(Name = "Є світло")]
    public bool HasElectricity { get; set; }

    [Display(Name = "Є зв'язок / інтернет")]
    public bool HasInternet { get; set; }

    [Display(Name = "Є опалення")]
    public bool HasHeating { get; set; }

    [Range(0, 10000)]
    [Display(Name = "Людей у пункті")]
    public int Occupancy { get; set; }

    [StringLength(1000)]
    [Display(Name = "Коментар")]
    public string? Comment { get; set; }

    [StringLength(300)]
    [Display(Name = "Фото")]
    public string? PhotoPath { get; set; }

    [StringLength(120)]
    [Display(Name = "Автор")]
    public string? AuthorName { get; set; }

    [Display(Name = "Час звіту")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
