namespace LabWeb.Models;

public enum GuideBlockType
{
    Paragraph = 0,
    Heading = 1,
    Bullet = 2
}

public class GuideBlock
{
    public Guid Id { get; set; }

    public int CommunityId { get; set; }
    public Community? Community { get; set; }

    public GuideBlockType Type { get; set; }

    public string Text { get; set; } = string.Empty;

    public double Position { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
