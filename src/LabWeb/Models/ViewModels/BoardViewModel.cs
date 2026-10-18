using LabWeb.Api.Dtos;

namespace LabWeb.Models.ViewModels;

public class BoardViewModel
{
    public string CommunityName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public IReadOnlyList<MapPointDto> Points { get; set; } = [];
}
