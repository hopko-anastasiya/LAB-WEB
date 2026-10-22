using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LabWeb.Assistant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LabWeb.Controllers;

public class ChatRequest
{
    [Required(ErrorMessage = "Напишіть повідомлення")]
    [StringLength(1000, ErrorMessage = "Повідомлення задовге: максимум 1000 символів")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Історія задовга")]
    public List<ChatMessage> History { get; set; } = [];
}

[Authorize]
public class AssistantController(AssistantAgent agent, IOptions<GeminiOptions> options, ILogger<AssistantController> logger) : Controller
{
    public const string RateLimitPolicy = "assistant";
    private const int MaxHistoryText = 4000;

    public IActionResult Index()
    {
        ViewData["enabled"] = options.Value.Enabled;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicy)]
    public async Task Chat([FromBody] ChatRequest request)
    {
        var valid = ModelState.IsValid && request.History.All(m => m.Role is "user" or "model" && m.Text?.Length <= MaxHistoryText);
        if (!valid || !options.Value.Enabled)
        {
            Response.StatusCode = valid ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest;
            return;
        }

        var ct = HttpContext.RequestAborted;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await agent.RunAsync(request.History, request.Message.Trim(), e => WriteAsync(e, ct), ct);
            await WriteAsync(new AssistantEvent("done", new { }), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is GeminiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Не вдалося отримати відповідь Gemini");
            var message = ex is GeminiException { Status: 429 }
                ? "Вичерпано ліміт запитів до моделі. Спробуйте за кілька хвилин."
                : "Модель зараз недоступна. Спробуйте пізніше.";
            await WriteAsync(new AssistantEvent("error", new { message }), ct);
        }
    }

    private async Task WriteAsync(AssistantEvent e, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {e.Type}\ndata: {JsonSerializer.Serialize(e.Data, PointTools.Json)}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
