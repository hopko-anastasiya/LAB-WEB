using System.Collections.Concurrent;
using LabWeb.Telegram;

namespace LabWeb.Tests.Infrastructure;

public record SentMessage(long ChatId, string Text, IReadOnlyList<IReadOnlyList<TgButton>>? Buttons);

public class FakeTelegramApi : ITelegramApi
{
    public ConcurrentQueue<SentMessage> Sent { get; } = new();

    public IReadOnlyList<SentMessage> SentTo(long chatId) => Sent.Where(m => m.ChatId == chatId).ToList();

    public Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TgButton>>? buttons = null, CancellationToken ct = default)
    {
        Sent.Enqueue(new SentMessage(chatId, text, buttons));
        return Task.CompletedTask;
    }

    public Task EditButtonsAsync(long chatId, long messageId, IReadOnlyList<IReadOnlyList<TgButton>> buttons, CancellationToken ct = default)
    {
        Sent.Enqueue(new SentMessage(chatId, "(кнопки оновлено)", buttons));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default) => Task.CompletedTask;

    public async Task<IReadOnlyList<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default)
    {
        await Task.Delay(Timeout.Infinite, ct);
        return [];
    }

    public Task SetWebhookAsync(string url, string? secret, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteWebhookAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<Stream> DownloadFileAsync(string fileId, CancellationToken ct = default)
    {
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        return Task.FromResult<Stream>(new MemoryStream(png));
    }
}
