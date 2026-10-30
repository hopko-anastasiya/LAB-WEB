namespace LabWeb.Telegram;

public record TgUpdate(long UpdateId, TgMessage? Message, TgCallbackQuery? CallbackQuery);

public record TgMessage(long MessageId, TgUser? From, TgChat Chat, string? Text, string? Caption, IReadOnlyList<TgPhotoSize>? Photo);

public record TgCallbackQuery(string Id, TgUser From, TgMessage? Message, string? Data);

public record TgUser(long Id, string? Username, string? FirstName);

public record TgChat(long Id);

public record TgPhotoSize(string FileId, int Width, int Height, long? FileSize);

public record TgFile(string FileId, string? FilePath, long? FileSize);

public record TgButton(string Text, string CallbackData);

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public string? SiteUrl { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);
}
