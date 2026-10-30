using System.Net;
using System.Net.Http.Json;
using LabWeb.Telegram;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class TelegramBotTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateAuthorizedClient();
    private static long _ids = Random.Shared.Next(1_000, 1_000_000);

    private async Task<HttpResponseMessage> Send(TgUpdate update, string secret = ApiFactory.TelegramSecret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/telegram/webhook")
        {
            Content = JsonContent.Create(update, options: TelegramApi.Json)
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Text(long chatId, string username, string text, IReadOnlyList<TgPhotoSize>? photo = null) =>
        Send(new TgUpdate(Interlocked.Increment(ref _ids),
            new TgMessage(Interlocked.Increment(ref _ids), new TgUser(chatId, username, "Тест"), new TgChat(chatId),
                photo is null ? text : null, photo is null ? null : text, photo),
            null));

    private Task<HttpResponseMessage> Press(long chatId, string username, string data) =>
        Send(new TgUpdate(Interlocked.Increment(ref _ids), null,
            new TgCallbackQuery(Guid.NewGuid().ToString(), new TgUser(chatId, username, "Тест"),
                new TgMessage(42, null, new TgChat(chatId), "", null, null), data)));

    private SentMessage Last(long chatId) => factory.Telegram.SentTo(chatId).Last();

    private async Task<(string Username, int OperatorId)> CreateTelegramOperator()
    {
        var username = $"tg_{Guid.NewGuid():N}"[..20];
        var r = await _client.PostJson("/api/operators", new { name = "Оператор із Telegram", phone = "+380501234567", telegram = "@" + username });
        return (username, (await r.ExpectJson(HttpStatusCode.Created))["id"]!.GetValue<int>());
    }

    [Fact]
    public async Task Webhook_rejects_wrong_secret()
    {
        var response = await Send(new TgUpdate(1, null, null), "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_is_asked_to_link_telegram()
    {
        var chatId = Interlocked.Increment(ref _ids);
        Assert.Equal(HttpStatusCode.OK, (await Text(chatId, "nobody_here", "/report")).StatusCode);
        Assert.Contains("не знайшов вас серед операторів", Last(chatId).Text);
    }

    [Fact]
    public async Task Operator_sends_report_with_photo_from_telegram()
    {
        var (username, operatorId) = await CreateTelegramOperator();
        var first = await _client.CreatePoint(operatorId);
        var second = await _client.CreatePoint(operatorId);
        var chatId = Interlocked.Increment(ref _ids);

        await Text(chatId, username, "/report");
        var choice = Last(chatId).Buttons!.SelectMany(r => r).Select(b => b.CallbackData).ToList();
        Assert.Equal(new[] { $"pt:{first}", $"pt:{second}" }.Order(), choice.Order());

        await Press(chatId, username, $"pt:{second}");
        await Press(chatId, username, "st:Limited");
        await Press(chatId, username, "fl:h");
        Assert.Contains(Last(chatId).Buttons!.SelectMany(r => r), b => b.CallbackData == "fl:h" && b.Text.StartsWith("✅"));
        await Press(chatId, username, "fl:done");

        await Text(chatId, username, "багато");
        Assert.Contains("числом", Last(chatId).Text);
        await Text(chatId, username, "15");
        await Text(chatId, username, "Генератор на ремонті", [new TgPhotoSize("file-1", 800, 600, 1000)]);

        Assert.StartsWith("Звіт збережено", Last(chatId).Text);
        var reports = await (await _client.GetAsync($"/api/points/{second}/reports")).ExpectJson(HttpStatusCode.OK);
        var report = reports["items"]![0]!;
        Assert.Equal("Limited", report["status"]!.GetValue<string>());
        Assert.Equal(15, report["occupancy"]!.GetValue<int>());
        Assert.True(report["hasHeating"]!.GetValue<bool>());
        Assert.Equal("Генератор на ремонті", report["comment"]!.GetValue<string>());
        Assert.NotNull(report["photoUrl"]);

        var point = await (await _client.GetAsync($"/api/points/{second}")).ExpectJson(HttpStatusCode.OK);
        Assert.Equal("Limited", point["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Operator_cannot_pick_someone_elses_point()
    {
        var (username, operatorId) = await CreateTelegramOperator();
        await _client.CreatePoint(operatorId);
        await _client.CreatePoint(operatorId);
        var foreign = await _client.CreatePoint(await _client.CreateOperator());
        var chatId = Interlocked.Increment(ref _ids);

        await Text(chatId, username, "/report");
        var before = factory.Telegram.SentTo(chatId).Count;
        await Press(chatId, username, $"pt:{foreign}");

        Assert.Equal(before, factory.Telegram.SentTo(chatId).Count);
    }
}
