using System.ComponentModel.DataAnnotations;

namespace LabWeb.Api.Dtos;

public record ServiceDto(int Id, string Name, string? Description, string Icon, int PointsCount);

public record ServiceRef(int Id, string Name, string Icon);

public class ServiceInput
{
    [Required(ErrorMessage = "Вкажіть назву послуги")]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [StringLength(48)]
    [RegularExpression(@"^bi-[a-z0-9-]+$", ErrorMessage = "Формат: bi-назва-іконки")]
    public string Icon { get; set; } = "bi-check-circle";
}
