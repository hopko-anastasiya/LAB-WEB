using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public class Operator
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть ім'я або назву")]
    [StringLength(120)]
    [Display(Name = "Ім'я / назва")]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Організація")]
    public string? Organization { get; set; }

    [Required(ErrorMessage = "Вкажіть телефон")]
    [Phone(ErrorMessage = "Некоректний номер телефону")]
    [StringLength(32)]
    [Display(Name = "Телефон")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Некоректний email")]
    [StringLength(160)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(64)]
    [Display(Name = "Telegram")]
    public string? Telegram { get; set; }

    [Display(Name = "Пункти")]
    public ICollection<Point> Points { get; set; } = new List<Point>();
}
