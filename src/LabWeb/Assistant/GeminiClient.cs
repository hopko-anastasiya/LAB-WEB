using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace LabWeb.Assistant;

public class GeminiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}

public class GeminiException(int status) : Exception($"Gemini повернув код {status}")
{
    public int Status { get; } = status;
}

public interface IGeminiClient
{
    IAsyncEnumerable<JsonObject> StreamAsync(JsonObject body, CancellationToken ct);
}

public class GeminiClient(HttpClient http, IOptions<GeminiOptions> options) : IGeminiClient
{
    public async IAsyncEnumerable<JsonObject> StreamAsync(JsonObject body, [EnumeratorCancellation] CancellationToken ct)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{options.Value.Model}:streamGenerateContent?alt=sse";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("x-goog-api-key", options.Value.ApiKey);

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new GeminiException((int)response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            if (JsonNode.Parse(line["data:".Length..]) is JsonObject chunk)
                yield return chunk;
        }
    }
}
