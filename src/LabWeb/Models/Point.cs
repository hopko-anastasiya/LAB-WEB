using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public class Point
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(120)]
    [Display(Name = "Назва")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(200)]
    [Display(Name = "Адреса")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Обов'язкове поле")]
    [StringLength(120)]
    [Display(Name = "Населений пункт")]
    public string Settlement { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Значення має бути від {1} до {2}")]
    [Display(Name = "Широта")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Значення має бути від {1} до {2}")]
    [Display(Name = "Довгота")]
    public double Longitude { get; set; }

    [StringLength(2000)]
    [Display(Name = "Опис")]
    public string? Description { get; set; }

    [StringLength(200)]
    [Display(Name = "Графік роботи")]
    public string? Schedule { get; set; }

    [Range(0, 10000, ErrorMessage = "Значення має бути від {1} до {2}")]
    [Display(Name = "Місткість, осіб")]
    public int Capacity { get; set; }

    [StringLength(300)]
    [Display(Name = "Фото")]
    public string? PhotoPath { get; set; }

    [Display(Name = "Статус")]
    public PointStatus Status { get; set; } = PointStatus.Working;

    [Display(Name = "Оператор")]
    public int OperatorId { get; set; }

    [Display(Name = "Оператор")]
    public Operator? Operator { get; set; }

    [Display(Name = "Послуги")]
    public ICollection<Service> Services { get; set; } = new List<Service>();

    [Display(Name = "Звіти про стан")]
    public ICollection<StatusReport> Reports { get; set; } = new List<StatusReport>();

    [Display(Name = "Створено")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Оновлено")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
