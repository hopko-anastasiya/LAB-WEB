using System.ComponentModel.DataAnnotations;

namespace LabWeb.Models;

public enum BoardItemKind
{
    Stroke = 0,
    Note = 1
}

public class BoardItem
{
    public int Id { get; set; }

    public int CommunityId { get; set; }
    public Community? Community { get; set; }

    public int? AuthorId { get; set; }
    public AppUser? Author { get; set; }

    [StringLength(120)]
    public string AuthorName { get; set; } = string.Empty;

    public BoardItemKind Kind { get; set; }

    [StringLength(7)]
    public string Color { get; set; } = string.Empty;

    public int Width { get; set; }

    public string Points { get; set; } = "[]";

    [StringLength(200)]
    public string? Text { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
