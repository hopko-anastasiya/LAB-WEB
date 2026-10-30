using Microsoft.Extensions.Options;

namespace LabWeb.Telegram;

public class TelegramBotService(
    IServiceScopeFactory scopes,
    ITelegramApi api,
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled) return;

        if (!string.IsNullOrWhiteSpace(o.WebhookUrl))
        {
            try
            {
                await api.SetWebhookAsync(o.WebhookUrl, o.WebhookSecret, stoppingToken);
                logger.LogInformation("Telegram-бот працює через вебхук {Url}", o.WebhookUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Не вдалося зареєструвати вебхук Telegram");
            }
            return;
        }

        logger.LogInformation("Telegram-бот працює через long polling");
        long offset = 0;
        var webhookDeleted = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!webhookDeleted)
                {
                    await api.DeleteWebhookAsync(stoppingToken);
                    webhookDeleted = true;
                }
                foreach (var update in await api.GetUpdatesAsync(offset, 25, stoppingToken))
                {
                    offset = update.UpdateId + 1;
                    await HandleAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Помилка отримання оновлень Telegram, повтор через 5 с");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(TgUpdate update, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportBot>().HandleAsync(update, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Не вдалося обробити оновлення Telegram {UpdateId}", update.UpdateId);
        }
    }
}
