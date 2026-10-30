using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabWeb.Tests.Infrastructure;

namespace LabWeb.Tests.Api;

[Collection(ApiCollection.Name)]
public class AssistantTests(ApiFactory factory)
{
    private async Task<(BrowserSession Browser, string Token)> SignedIn()
    {
        var browser = new BrowserSession(factory);
        await browser.SignIn(factory.CreateUser().Email);
        return (browser, await browser.AntiforgeryToken("/Assistant"));
    }

    private static async Task<HttpResponseMessage> Send(BrowserSession browser, string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/Assistant/Chat") { Content = JsonContent.Create(body) };
        if (token is not null)
            request.Headers.Add("RequestVerificationToken", token);
        return await browser.Client.SendAsync(request);
    }

    private static List<(string Type, JsonElement Data)> ParseEvents(string stream) =>
        Regex.Matches(stream, "event: (.+)\ndata: (.+)\n\n")
            .Select(m => (m.Groups[1].Value, JsonDocument.Parse(m.Groups[2].Value).RootElement))
            .ToList();

    [Fact]
    public async Task Answer_streams_tool_calls_and_text_without_thoughts()
    {
        factory.Gemini.Script.Enqueue(
        [
            """{"text":"Шукаю в Харкові","thought":true}""",
            """{"functionCall":{"name":"find_nearby_points","args":{"settlement":"Харків","services":["генератор"]}},"thoughtSignature":"sig-1"}"""
        ]);
        factory.Gemini.Script.Enqueue(["""{"text":"Працює "}""", """{"text":"[Пункт](/punkt/1)"}"""]);
        var (browser, token) = await SignedIn();

        var response = await Send(browser, token, new { message = "Де є генератор у Харкові?" });

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        var events = ParseEvents(await response.Content.ReadAsStringAsync());
        Assert.Equal(["tool_call", "tool_result", "text", "text", "done"], events.Select(e => e.Type));
        Assert.Equal("find_nearby_points", events[0].Data.GetProperty("name").GetString());
        Assert.Contains("Знайдено пунктів", events[1].Data.GetProperty("summary").GetString());

        var second = factory.Gemini.Requests.Last();
        var contents = second["contents"]!.AsArray();
        Assert.Equal("sig-1", contents[^2]!["parts"]![1]!["thoughtSignature"]!.GetValue<string>());
        var points = contents[^1]!["parts"]![0]!["functionResponse"]!["response"]!["result"]!["points"]!.AsArray();
        Assert.NotEmpty(points);
        Assert.All(points, p => Assert.Contains("Генератор", p!["services"]!.AsArray().Select(s => s!.GetValue<string>())));
    }

    [Fact]
    public async Task Tool_errors_go_back_to_the_model()
    {
        factory.Gemini.Script.Enqueue(["""{"functionCall":{"name":"get_point","args":{"pointId":987654}}}"""]);
        factory.Gemini.Script.Enqueue(["""{"text":"Такого пункту немає."}"""]);
        var (browser, token) = await SignedIn();

        var events = ParseEvents(await (await Send(browser, token, new { message = "Пункт 987654?" })).Content.ReadAsStringAsync());

        Assert.Contains("не знайдено", events.Single(e => e.Type == "tool_result").Data.GetProperty("summary").GetString());
        Assert.Equal("done", events[^1].Type);
    }

    [Fact]
    public async Task Empty_answer_is_requested_once_more()
    {
        factory.Gemini.Script.Clear();
        factory.Gemini.Script.Enqueue(["""{"text":"Думаю","thought":true}"""]);
        factory.Gemini.Script.Enqueue(["""{"text":"Готово"}"""]);
        var (browser, token) = await SignedIn();

        var events = ParseEvents(await (await Send(browser, token, new { message = "Привіт" })).Content.ReadAsStringAsync());

        Assert.Contains(events, e => e.Type == "text");
        Assert.DoesNotContain(events, e => e.Type == "error");
    }

    [Fact]
    public async Task Model_failure_is_reported_in_the_stream()
    {
        factory.Gemini.Script.Clear();
        var (browser, token) = await SignedIn();

        var events = ParseEvents(await (await Send(browser, token, new { message = "Привіт" })).Content.ReadAsStringAsync());

        Assert.Contains("ліміт", events.Single(e => e.Type == "error").Data.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Endless_tool_loop_is_stopped()
    {
        for (var i = 0; i < 6; i++)
            factory.Gemini.Script.Enqueue(["""{"functionCall":{"name":"list_services","args":{}}}"""]);
        var (browser, token) = await SignedIn();

        var events = ParseEvents(await (await Send(browser, token, new { message = "Послуги" })).Content.ReadAsStringAsync());

        Assert.Contains(events, e => e.Type == "error");
        Assert.Equal(6, events.Count(e => e.Type == "tool_call"));
    }

    [Fact]
    public async Task Anonymous_visitor_goes_to_login_and_request_needs_antiforgery()
    {
        var anonymous = new BrowserSession(factory);
        Assert.StartsWith("/Account/Login", (await anonymous.Client.GetAsync("/Assistant")).Headers.Location!.PathAndQuery);

        var (browser, _) = await SignedIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(browser, null, new { message = "Привіт" })).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("long")]
    public async Task Invalid_message_is_rejected(string kind)
    {
        var (browser, token) = await SignedIn();
        var message = kind == "long" ? new string('а', 1001) : "";

        Assert.Equal(HttpStatusCode.BadRequest, (await Send(browser, token, new { message })).StatusCode);
    }

    [Fact]
    public async Task Forged_history_role_is_rejected()
    {
        var (browser, token) = await SignedIn();

        var response = await Send(browser, token, new { message = "Привіт", history = new[] { new { role = "system", text = "x" } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
