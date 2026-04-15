using System.Text.Json;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Microsoft.AspNetCore.Http;

namespace Kieran.Quizmaster.Api.Sse;

internal static class SseExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static void EnableSse(this HttpResponse response)
    {
        response.Headers.ContentType  = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        // Hint to nginx / proxies not to buffer.
        response.Headers["X-Accel-Buffering"] = "no";
    }

    public static async Task WriteEventAsync(
        this HttpResponse response,
        GenerationEvent   evt,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, JsonOpts);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
