namespace LabWeb.Services;

public class LocalFileStorage(IWebHostEnvironment env, UploadRules rules) : IFileStorage
{
    private const string RootFolder = "uploads";

    public string? Validate(IFormFile file) => rules.Validate(file);

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var dir = Path.Combine(env.WebRootPath, RootFolder, folder);
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using (var stream = File.Create(Path.Combine(dir, name)))
        {
            await file.CopyToAsync(stream, ct);
        }
        return $"/{RootFolder}/{folder}/{name}";
    }

    public Task DeleteAsync(string? path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith($"/{RootFolder}/"))
            return Task.CompletedTask;

        var full = Path.GetFullPath(Path.Combine(env.WebRootPath, path.TrimStart('/')));
        var root = Path.GetFullPath(Path.Combine(env.WebRootPath, RootFolder));
        if (full.StartsWith(root) && File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }
}
