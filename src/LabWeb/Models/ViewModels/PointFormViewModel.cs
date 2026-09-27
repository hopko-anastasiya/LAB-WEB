using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LabWeb.Models.ViewModels;

public class PointFormViewModel
{
    public Point Point { get; set; } = new();

    [Display(Name = "Фото пункту")]
    public IFormFile? Photo { get; set; }

    [Display(Name = "Видалити поточне фото")]
    public bool RemovePhoto { get; set; }

    [Display(Name = "Послуги")]
    public List<int> SelectedServiceIds { get; set; } = [];

    public IEnumerable<SelectListItem> Operators { get; set; } = [];
    public IEnumerable<Service> AllServices { get; set; } = [];
}
