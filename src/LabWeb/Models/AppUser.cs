using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public enum CommunityRole
{
    [Display(Name = "Адміністратор")]
    Admin = 0,

    [Display(Name = "Учасник")]
    Member = 1
}

public class AppUser
{
    public int Id { get; set; }

    [StringLength(64)]
    public string? GoogleId { get; set; }

    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? PictureUrl { get; set; }

    public int? CommunityId { get; set; }
    public Community? Community { get; set; }

    public CommunityRole Role { get; set; } = CommunityRole.Member;

    [StringLength(64)]
    public string? ApiTokenHash { get; set; }

    public DateTime? ApiTokenCreatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
}
