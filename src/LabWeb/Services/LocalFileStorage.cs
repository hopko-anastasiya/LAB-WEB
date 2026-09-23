namespace LabWeb.Services;

public class LocalFileStorage : IFileStorage
{
    private const string RootFolder = "uploads";

    private readonly IWebHostEnvironment _env;
    private readonly long _maxBytes;
    private readonly HashSet<string> _allowed;

    public LocalFileStorage(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _maxBytes = config.GetValue("Uploads:MaxFileSizeMb", 5) * 1024L * 1024L;
        var ext = config.GetSection("Uploads:AllowedExtensions").Get<string[]>()
                  ?? [".jpg", ".jpeg", ".png", ".webp"];
        _allowed = new HashSet<string>(ext, StringComparer.OrdinalIgnoreCase);
    }

    public string? Validate(IFormFile file)
    {
        if (file.Length == 0)
            return "Файл порожній.";
        if (file.Length > _maxBytes)
            return $"Файл завеликий (максимум {_maxBytes / 1024 / 1024} МБ).";
        var ext = Path.GetExtension(file.FileName);
        if (!_allowed.Contains(ext))
            return $"Дозволені формати: {string.Join(", ", _allowed)}.";
        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Файл має бути зображенням.";
        return null;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var dir = Path.Combine(_env.WebRootPath, RootFolder, folder);
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

        var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, path.TrimStart('/')));
        var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, RootFolder));
        if (full.StartsWith(root) && File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }
}
