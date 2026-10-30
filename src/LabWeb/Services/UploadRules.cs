namespace LabWeb.Services;

public class UploadRules
{
    private readonly long _maxBytes;
    private readonly HashSet<string> _allowed;

    public UploadRules(IConfiguration config)
    {
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
}
