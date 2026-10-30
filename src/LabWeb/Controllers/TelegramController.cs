using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LabWeb.Telegram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LabWeb.Controllers;

[ApiController]
[Route("telegram")]
public class TelegramController(ReportBot bot, IOptions<TelegramOptions> options, ILogger<TelegramController> logger) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var secret = options.Value.WebhookSecret;
        if (!options.Value.Enabled || string.IsNullOrEmpty(secret))
            return NotFound();

        var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(secret)))
            return Unauthorized();

        var update = await JsonSerializer.DeserializeAsync<TgUpdate>(Request.Body, TelegramApi.Json, ct);
        if (update is null)
            return BadRequest();

        try
        {
            await bot.HandleAsync(update, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Не вдалося обробити оновлення Telegram {UpdateId}", update.UpdateId);
        }
        return Ok();
    }
}
