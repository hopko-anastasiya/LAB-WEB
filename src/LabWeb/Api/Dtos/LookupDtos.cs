using System.ComponentModel.DataAnnotations;

namespace LabWeb.Api.Dtos;

public record SettlementDto(int Id, string Name, string Region, double Latitude, double Longitude, int Population);

public record LookupItem(int Id, string Text, string? Hint);

public class LookupQuery
{
    public const int MinLength = 3;

    [Required(ErrorMessage = "Введіть щонайменше 3 символи")]
    [MinLength(MinLength, ErrorMessage = "Введіть щонайменше 3 символи")]
    [StringLength(100, ErrorMessage = "Запит задовгий")]
    public string Q { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "limit має бути від {1} до {2}")]
    public int Limit { get; set; } = 20;

    public string[] Words() =>
        Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Select(w => $"%{Escape(w)}%")
         .ToArray();

    private static string Escape(string word) =>
        word.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
