namespace LabWeb.Services;

public interface IFileStorage
{
    string? Validate(IFormFile file);

    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);

    Task DeleteAsync(string? path, CancellationToken ct = default);
}
