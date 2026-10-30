namespace LabWeb.Services;

public class UploadRules
{
    private static readonly byte[][] Signatures =
    [
        [0xFF, 0xD8, 0xFF],
        [0x89, 0x50, 0x4E, 0x47],
        [0x52, 0x49, 0x46, 0x46]
    ];

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
        if (!HasImageSignature(file))
            return "Вміст файлу не схожий на зображення JPEG, PNG або WebP.";
        return null;
    }

    private static bool HasImageSignature(IFormFile file)
    {
        var header = new byte[4];
        using var stream = file.OpenReadStream();
        if (stream.ReadAtLeast(header, 4, throwOnEndOfStream: false) < 4) return false;
        return Signatures.Any(s => header.AsSpan().StartsWith(s));
    }
}
