using System.ComponentModel.DataAnnotations;

namespace LabWeb.Api;

public class PageQuery
{
    public const int MaxLimit = 100;

    [Range(0, int.MaxValue, ErrorMessage = "skip не може бути від'ємним")]
    public int Skip { get; set; }

    [Range(1, MaxLimit, ErrorMessage = "limit має бути від {1} до {2}")]
    public int Limit { get; set; } = 20;
}
