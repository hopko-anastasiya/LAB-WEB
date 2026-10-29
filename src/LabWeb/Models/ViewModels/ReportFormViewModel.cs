using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LabWeb.Models.ViewModels;

public class ReportFormViewModel
{
    public StatusReport Report { get; set; } = new();

    [Display(Name = "Фото")]
    public IFormFile? Photo { get; set; }

    [Display(Name = "Видалити поточне фото")]
    public bool RemovePhoto { get; set; }

    public IEnumerable<SelectListItem> Points { get; set; } = [];
}
