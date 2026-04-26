using System.Runtime.CompilerServices;
using System.Text.Json;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

/// <summary>
/// Pure JSON parser — no AI calls. Used by the BYO-AI / "Generate
/// elsewhere, paste back" flow.
/// </summary>
public sealed class QuizJsonImporter : IQuizJsonImporter
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async IAsyncEnumerable<GenerationEvent> ImportFromJsonAsync(
        ImportFromJsonRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // async marker so this is a true IAsyncEnumerable; we don't actually
        // need to await anything — the work is all CPU-bound JSON parsing.
        await Task.Yield();
        ct.ThrowIfCancellationRequested();

        yield return new GenerationEvent.Status("parsing");

        if (string.IsNullOrWhiteSpace(request.SourceJson))
        {
            yield return new GenerationEvent.Error(
                "No JSON pasted — paste the AI's response into the box.", Retryable: false);
            yield break;
        }

        var text = ExtractJson(request.SourceJson);

        RawQuizResponse? parsed = null;
        string?          parseError = null;
        try
        {
            parsed = JsonSerializer.Deserialize<RawQuizResponse>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            parseError = $"Couldn't parse JSON: {ex.Message}";
        }

        if (parseError is not null)
        {
            yield return new GenerationEvent.Error(parseError, Retryable: false);
            yield break;
        }
        if (parsed is null || parsed.Questions is null || parsed.Questions.Count == 0)
        {
            yield return new GenerationEvent.Error(
                "JSON parsed but contained no questions.", Retryable: false);
            yield break;
        }

        var collected = parsed.Questions
            .Select((q, i) => QuizGenerator.ToDraft(q, i))
            .ToList();

        foreach (var dq in collected)
        {
            yield return new GenerationEvent.Question(dq);
        }

        // Mirror the partial-count warnings emitted by the AI flow when the
        // user supplied topic specs.
        if (request.Topics.Count > 0)
        {
            var byTopic = collected
                .GroupBy(q => q.Topic, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var t in request.Topics)
            {
                var got = byTopic.GetValueOrDefault(t.Name, 0);
                if (got < t.Count)
                {
                    yield return new GenerationEvent.Warning(
                        $"Asked for {t.Count} question{(t.Count == 1 ? "" : "s")} on '{t.Name}', got {got}.");
                }
            }
        }

        var draft = new DraftQuiz(
            request.Title, "Imported", "Manual", "Manual",
            request.Topics, request.SourceJson, collected);

        yield return new GenerationEvent.Done(draft);
    }

    /// <summary>
    /// Defensive: trim markdown fences and surrounding prose. Slice from the
    /// first '{' to the last '}' so JsonSerializer has a fair shot. Shared
    /// with <see cref="FactCheckJsonMerger"/>.
    /// </summary>
    internal static string ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text[start..(end + 1)] : text;
    }
}
