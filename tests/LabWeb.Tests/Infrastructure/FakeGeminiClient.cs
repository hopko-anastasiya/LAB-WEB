using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using LabWeb.Assistant;

namespace LabWeb.Tests.Infrastructure;

public class FakeGeminiClient : IGeminiClient
{
    public ConcurrentQueue<string[]> Script { get; } = new();
    public ConcurrentQueue<JsonObject> Requests { get; } = new();

    public async IAsyncEnumerable<JsonObject> StreamAsync(JsonObject body, [EnumeratorCancellation] CancellationToken ct)
    {
        Requests.Enqueue(body);
        if (!Script.TryDequeue(out var parts))
            throw new GeminiException(429);

        foreach (var part in parts)
        {
            await Task.Yield();
            yield return JsonNode.Parse("""{"candidates":[{"content":{"role":"model","parts":[""" + part + "]}}]}")!.AsObject();
        }
    }
}
