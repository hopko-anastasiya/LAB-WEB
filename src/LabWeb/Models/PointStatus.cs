using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public enum PointStatus
{
    [Display(Name = "Працює")]
    Working = 0,

    [Display(Name = "Працює з обмеженнями")]
    Limited = 1,

    [Display(Name = "Не працює")]
    Closed = 2
}
