using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public class Service
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву послуги")]
    [StringLength(80)]
    [Display(Name = "Назва")]
    public string Name { get; set; } = string.Empty;

    [StringLength(400)]
    [Display(Name = "Опис")]
    public string? Description { get; set; }

    [StringLength(48)]
    [RegularExpression(@"^bi-[a-z0-9-]+$", ErrorMessage = "Формат: bi-назва-іконки")]
    [Display(Name = "Іконка")]
    public string Icon { get; set; } = "bi-check-circle";

    public ICollection<Point> Points { get; set; } = new List<Point>();
}
