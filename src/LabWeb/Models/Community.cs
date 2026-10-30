using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public enum Plan
{
    [Display(Name = "Базовий")]
    Free = 0,

    [Display(Name = "Premium")]
    Premium = 1
}

public class Community
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Вкажіть назву громади")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "Назва має містити від {2} до {1} символів")]
    [Display(Name = "Назва громади")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "План")]
    public Plan Plan { get; set; } = Plan.Free;

    [StringLength(16)]
    [Display(Name = "Код запрошення")]
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Operator> Operators { get; set; } = new List<Operator>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
