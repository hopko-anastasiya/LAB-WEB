using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LabWeb.Telegram;

public interface ITelegramApi
{
    Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TgButton>>? buttons = null, CancellationToken ct = default);
    Task EditButtonsAsync(long chatId, long messageId, IReadOnlyList<IReadOnlyList<TgButton>> buttons, CancellationToken ct = default);
    Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default);
    Task<IReadOnlyList<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default);
    Task SetWebhookAsync(string url, string? secret, CancellationToken ct = default);
    Task DeleteWebhookAsync(CancellationToken ct = default);
    Task<Stream> DownloadFileAsync(string fileId, CancellationToken ct = default);
}

public class TelegramApi(HttpClient http, IOptions<TelegramOptions> options) : ITelegramApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string Token => options.Value.BotToken ?? throw new InvalidOperationException("Не задано Telegram:BotToken");

    public Task SendMessageAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<TgButton>>? buttons = null, CancellationToken ct = default) =>
        CallAsync<JsonElement>("sendMessage", new
        {
            chat_id = chatId,
            text,
            parse_mode = "HTML",
            link_preview_options = new { is_disabled = true },
            reply_markup = buttons is null ? null : Keyboard(buttons)
        }, ct);

    public Task EditButtonsAsync(long chatId, long messageId, IReadOnlyList<IReadOnlyList<TgButton>> buttons, CancellationToken ct = default) =>
        CallAsync<JsonElement>("editMessageReplyMarkup", new { chat_id = chatId, message_id = messageId, reply_markup = Keyboard(buttons) }, ct);

    public Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default) =>
        CallAsync<JsonElement>("answerCallbackQuery", new { callback_query_id = callbackId }, ct);

    public async Task<IReadOnlyList<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default) =>
        await CallAsync<List<TgUpdate>>("getUpdates", new { offset, timeout = timeoutSeconds, allowed_updates = new[] { "message", "callback_query" } }, ct);

    public Task SetWebhookAsync(string url, string? secret, CancellationToken ct = default) =>
        CallAsync<JsonElement>("setWebhook", new { url, secret_token = secret, allowed_updates = new[] { "message", "callback_query" } }, ct);

    public Task DeleteWebhookAsync(CancellationToken ct = default) =>
        CallAsync<JsonElement>("deleteWebhook", new { }, ct);

    public async Task<Stream> DownloadFileAsync(string fileId, CancellationToken ct = default)
    {
        var file = await CallAsync<TgFile>("getFile", new { file_id = fileId }, ct);
        if (file.FilePath is null)
            throw new InvalidOperationException("Telegram не повернув шлях до файлу");
        return await http.GetStreamAsync($"https://api.telegram.org/file/bot{Token}/{file.FilePath}", ct);
    }

    private static object Keyboard(IReadOnlyList<IReadOnlyList<TgButton>> buttons) => new
    {
        inline_keyboard = buttons.Select(row => row.Select(b => new { text = b.Text, callback_data = b.CallbackData }))
    };

    private async Task<T> CallAsync<T>(string method, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"https://api.telegram.org/bot{Token}/{method}", body, Json, ct);
        var result = await response.Content.ReadFromJsonAsync<TgResponse<T>>(Json, ct);
        if (result is not { Ok: true, Result: not null })
            throw new HttpRequestException($"Telegram {method}: {result?.Description ?? response.ReasonPhrase}");
        return result.Result;
    }

    private record TgResponse<T>(bool Ok, T? Result, string? Description);
}
