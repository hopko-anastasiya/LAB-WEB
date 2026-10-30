using System.ComponentModel.DataAnnotations;

namespace LabWeb.Api.Dtos;

public record OperatorDto(int Id, string Name, string? Organization, string Phone, string? Email, string? Telegram, int PointsCount);

public record OperatorRef(int Id, string Name, string Phone);

public class OperatorInput
{
    [Required(ErrorMessage = "Вкажіть ім'я або назву")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string? Organization { get; set; }

    [Required(ErrorMessage = "Вкажіть телефон")]
    [Phone(ErrorMessage = "Некоректний номер телефону")]
    [StringLength(32)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Некоректний email")]
    [StringLength(160)]
    public string? Email { get; set; }

    [StringLength(64)]
    public string? Telegram { get; set; }
}
