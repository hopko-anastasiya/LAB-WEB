using System.ComponentModel.DataAnnotations;
using LabWeb.Models;

namespace LabWeb.Api.Dtos;

public record SearchHitDto(
    int Id,
    string Name,
    string Settlement,
    string Address,
    PointStatus Status,
    IReadOnlyList<string> Services,
    float Score);

public class SearchQuery : PageQuery
{
    [Required(ErrorMessage = "Введіть пошуковий запит")]
    [MinLength(2, ErrorMessage = "Запит має містити щонайменше 2 символи")]
    [StringLength(200, ErrorMessage = "Запит задовгий")]
    public string Q { get; set; } = string.Empty;

    public PointStatus? Status { get; set; }
}
