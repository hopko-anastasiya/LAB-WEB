using System.Collections.Concurrent;
using System.Net;
using LabWeb.Data;
using LabWeb.Infrastructure;
using LabWeb.Models;
using LabWeb.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LabWeb.Telegram;

public class ReportDraft
{
    public ReportStep Step { get; set; } = ReportStep.Point;
    public int PointId { get; set; }
    public PointStatus Status { get; set; }
    public bool HasElectricity { get; set; }
    public bool HasInternet { get; set; }
    public bool HasHeating { get; set; }
    public int Occupancy { get; set; }
}

public enum ReportStep { Point, Status, Flags, Occupancy, Comment }

public class BotSessions : ConcurrentDictionary<long, ReportDraft>;

public class ReportBot(
    AppDbContext db,
    ITelegramApi api,
    BotSessions sessions,
    IFileStorage files,
    PointStatusService statuses,
    IOptions<TelegramOptions> options)
{
    private const string Help =
        "Я приймаю звіти про стан пунктів незламності.\n\n" +
        "/report — надіслати звіт\n" +
        "/points — мої пункти та їхні статуси\n" +
        "/cancel — скасувати звіт";

    public async Task HandleAsync(TgUpdate update, CancellationToken ct = default)
    {
        if (update.CallbackQuery is { Message: not null } callback)
        {
            await api.AnswerCallbackAsync(callback.Id, ct);
            await HandleCallbackAsync(callback, ct);
        }
        else if (update.Message is { From: not null } message)
        {
            await HandleMessageAsync(message, ct);
        }
    }

    private async Task HandleMessageAsync(TgMessage message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var command = message.Text?.Trim().Split(' ', '@')[0].ToLowerInvariant();

        if (command is "/start" or "/help")
        {
            var op = await FindOperatorAsync(message.From!, ct);
            var hello = op is null ? "" : $"Вітаю, {Html(op.Name)}!\n\n";
            await api.SendMessageAsync(chatId, hello + Help, ct: ct);
            if (op is null) await SendUnknownAsync(chatId, ct);
            return;
        }

        var @operator = await FindOperatorAsync(message.From!, ct);
        if (@operator is null)
        {
            await SendUnknownAsync(chatId, ct);
            return;
        }

        switch (command)
        {
            case "/points":
                await SendPointsAsync(chatId, @operator, ct);
                return;
            case "/report":
                await StartReportAsync(chatId, @operator, ct);
                return;
            case "/cancel":
                sessions.TryRemove(chatId, out _);
                await api.SendMessageAsync(chatId, "Звіт скасовано.", ct: ct);
                return;
        }

        if (!sessions.TryGetValue(chatId, out var draft))
        {
            await api.SendMessageAsync(chatId, "Щоб надіслати звіт, напишіть /report.", ct: ct);
            return;
        }

        switch (draft.Step)
        {
            case ReportStep.Occupancy:
                if (!int.TryParse(message.Text?.Trim(), out var people) || people is < 0 or > 10000)
                {
                    await api.SendMessageAsync(chatId, "Надішліть кількість людей числом від 0 до 10000.", ct: ct);
                    return;
                }
                draft.Occupancy = people;
                draft.Step = ReportStep.Comment;
                await api.SendMessageAsync(chatId, "Додайте коментар або фото пункту (можна фото з підписом).",
                    [[new TgButton("Зберегти без коментаря", "skip")]], ct);
                return;
            case ReportStep.Comment:
                await SaveAsync(chatId, @operator, draft, message.Text ?? message.Caption, message.Photo, ct);
                return;
            default:
                await api.SendMessageAsync(chatId, "Скористайтеся кнопками під попереднім повідомленням або напишіть /cancel.", ct: ct);
                return;
        }
    }

    private async Task HandleCallbackAsync(TgCallbackQuery callback, CancellationToken ct)
    {
        var chatId = callback.Message!.Chat.Id;
        var @operator = await FindOperatorAsync(callback.From, ct);
        if (@operator is null)
        {
            await SendUnknownAsync(chatId, ct);
            return;
        }
        if (!sessions.TryGetValue(chatId, out var draft))
        {
            await api.SendMessageAsync(chatId, "Цей звіт уже завершено. Щоб почати новий, напишіть /report.", ct: ct);
            return;
        }

        var (kind, value) = Split(callback.Data);
        switch (kind)
        {
            case "pt" when draft.Step == ReportStep.Point && int.TryParse(value, out var pointId):
                var point = @operator.Points.FirstOrDefault(p => p.Id == pointId);
                if (point is null) return;
                await AskStatusAsync(chatId, draft, point, ct);
                return;
            case "st" when draft.Step == ReportStep.Status && Enum.TryParse<PointStatus>(value, out var status):
                draft.Status = status;
                draft.HasElectricity = status != PointStatus.Closed;
                draft.HasInternet = status != PointStatus.Closed;
                draft.HasHeating = status == PointStatus.Working;
                draft.Step = ReportStep.Flags;
                await api.SendMessageAsync(chatId, "Що зараз є в пункті? Натисніть, щоб змінити, і потім «Далі».", FlagButtons(draft), ct);
                return;
            case "fl" when draft.Step == ReportStep.Flags:
                if (value == "done")
                {
                    draft.Step = ReportStep.Occupancy;
                    await api.SendMessageAsync(chatId, "Скільки людей зараз у пункті? Надішліть число.", ct: ct);
                    return;
                }
                if (value == "e") draft.HasElectricity = !draft.HasElectricity;
                if (value == "i") draft.HasInternet = !draft.HasInternet;
                if (value == "h") draft.HasHeating = !draft.HasHeating;
                await api.EditButtonsAsync(chatId, callback.Message.MessageId, FlagButtons(draft), ct);
                return;
            case "skip" when draft.Step == ReportStep.Comment:
                await SaveAsync(chatId, @operator, draft, null, null, ct);
                return;
        }
    }

    private async Task StartReportAsync(long chatId, Operator @operator, CancellationToken ct)
    {
        var points = @operator.Points.OrderBy(p => p.Name).ToList();
        if (points.Count == 0)
        {
            await api.SendMessageAsync(chatId, "За вами ще не закріплено жодного пункту.", ct: ct);
            return;
        }

        var draft = new ReportDraft();
        sessions[chatId] = draft;
        if (points.Count == 1)
        {
            await AskStatusAsync(chatId, draft, points[0], ct);
            return;
        }
        await api.SendMessageAsync(chatId, "Про який пункт звіт?",
            points.Select(p => (IReadOnlyList<TgButton>)[new TgButton($"{Lamp(p.Status)} {p.Name}", $"pt:{p.Id}")]).ToList(), ct);
    }

    private async Task AskStatusAsync(long chatId, ReportDraft draft, Point point, CancellationToken ct)
    {
        draft.PointId = point.Id;
        draft.Step = ReportStep.Status;
        await api.SendMessageAsync(chatId, $"<b>{Html(point.Name)}</b>\nЯк зараз працює пункт?",
        [
            [new TgButton($"{Lamp(PointStatus.Working)} Працює", "st:Working")],
            [new TgButton($"{Lamp(PointStatus.Limited)} З обмеженнями", "st:Limited")],
            [new TgButton($"{Lamp(PointStatus.Closed)} Не працює", "st:Closed")]
        ], ct);
    }

    private async Task SaveAsync(long chatId, Operator @operator, ReportDraft draft, string? comment, IReadOnlyList<TgPhotoSize>? photo, CancellationToken ct)
    {
        var point = await db.Points.FirstOrDefaultAsync(p => p.Id == draft.PointId && p.OperatorId == @operator.Id, ct);
        if (point is null)
        {
            sessions.TryRemove(chatId, out _);
            await api.SendMessageAsync(chatId, "Пункт більше недоступний. Почніть заново: /report.", ct: ct);
            return;
        }

        string? photoPath = null;
        if (photo is { Count: > 0 })
        {
            var (path, error) = await SavePhotoAsync(photo.MaxBy(p => p.Width * p.Height)!, ct);
            if (error is not null)
            {
                await api.SendMessageAsync(chatId, $"{error} Надішліть інше фото або натисніть «Зберегти без коментаря».",
                    [[new TgButton("Зберегти без коментаря", "skip")]], ct);
                return;
            }
            photoPath = path;
        }

        comment = comment?.Trim();
        var report = new StatusReport
        {
            PointId = point.Id,
            Status = draft.Status,
            HasElectricity = draft.HasElectricity,
            HasInternet = draft.HasInternet,
            HasHeating = draft.HasHeating,
            Occupancy = draft.Occupancy,
            Comment = string.IsNullOrEmpty(comment) ? null : comment[..Math.Min(comment.Length, 1000)],
            PhotoPath = photoPath,
            AuthorName = $"{@operator.Name} (Telegram)",
            CreatedAt = DateTime.UtcNow
        };
        db.StatusReports.Add(report);
        statuses.ApplyNewReport(point, report);
        await db.SaveChangesAsync(ct);
        sessions.TryRemove(chatId, out _);

        var link = string.IsNullOrWhiteSpace(options.Value.SiteUrl)
            ? ""
            : $"\n{options.Value.SiteUrl.TrimEnd('/')}/punkt/{point.Id}";
        await api.SendMessageAsync(chatId,
            $"Звіт збережено. «{Html(point.Name)}»: {Lamp(point.Status)} {point.Status.DisplayName()}.{link}", ct: ct);
    }

    private async Task<(string? Path, string? Error)> SavePhotoAsync(TgPhotoSize photo, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await using (var source = await api.DownloadFileAsync(photo.FileId, ct))
            await source.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var file = new FormFile(buffer, 0, buffer.Length, "photo", "telegram.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var error = files.Validate(file);
        return error is not null ? (null, error) : (await files.SaveAsync(file, "reports", ct), null);
    }

    private async Task SendPointsAsync(long chatId, Operator @operator, CancellationToken ct)
    {
        var lines = @operator.Points.OrderBy(p => p.Name)
            .Select(p => $"{Lamp(p.Status)} <b>{Html(p.Name)}</b>\n{Html(p.Settlement)}, {Html(p.Address)}\n{p.Status.DisplayName()}, оновлено {p.UpdatedAt.ToLocalString()}");
        var text = @operator.Points.Count == 0 ? "За вами ще не закріплено жодного пункту." : string.Join("\n\n", lines);
        await api.SendMessageAsync(chatId, text, ct: ct);
    }

    private Task SendUnknownAsync(long chatId, CancellationToken ct) =>
        api.SendMessageAsync(chatId,
            "Я не знайшов вас серед операторів. Попросіть додати ваш Telegram-нікнейм (@username) у профіль оператора на сайті.", ct: ct);

    private async Task<Operator?> FindOperatorAsync(TgUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Username)) return null;
        var username = user.Username.ToLowerInvariant();
        return await db.Operators.Include(o => o.Points)
            .FirstOrDefaultAsync(o => o.Telegram != null
                && (o.Telegram.ToLower() == username || o.Telegram.ToLower() == "@" + username), ct);
    }

    private static IReadOnlyList<IReadOnlyList<TgButton>> FlagButtons(ReportDraft d) =>
    [
        [new TgButton($"{Check(d.HasElectricity)} Світло", "fl:e")],
        [new TgButton($"{Check(d.HasInternet)} Зв'язок / інтернет", "fl:i")],
        [new TgButton($"{Check(d.HasHeating)} Тепло", "fl:h")],
        [new TgButton("Далі", "fl:done")]
    ];

    private static (string Kind, string Value) Split(string? data)
    {
        var parts = (data ?? "").Split(':', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "");
    }

    private static string Check(bool on) => on ? "✅" : "❌";

    private static string Lamp(PointStatus status) => status switch
    {
        PointStatus.Working => "🟡",
        PointStatus.Limited => "🟠",
        _ => "⚫"
    };

    private static string Html(string? text) => WebUtility.HtmlEncode(text ?? "");
}
