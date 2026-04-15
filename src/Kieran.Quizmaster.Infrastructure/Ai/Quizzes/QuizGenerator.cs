using System.Runtime.CompilerServices;
using System.Text.Json;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

public sealed class QuizGenerator(
    IAiChatClientFactory factory,
    IFactChecker         factChecker) : IQuizGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async IAsyncEnumerable<GenerationEvent> GenerateAsync(
        GenerateQuizRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new GenerationEvent.Status("generating");

        if (!AiProviderKind.TryFromName(request.Provider, out var providerKind))
        {
            yield return new GenerationEvent.Error(
                $"Unknown provider '{request.Provider}'.", Retryable: false);
            yield break;
        }

        IChatClient? client = null;
        string?      createError = null;
        try   { client = factory.Create(providerKind, request.Model); }
        catch (Exception ex) { createError = ex.Message; }
        if (client is null)
        {
            yield return new GenerationEvent.Error(createError ?? "Failed to create AI client.", Retryable: false);
            yield break;
        }

        var prompt = Prompts.Generation(request.Topics, request.MultipleChoiceFraction);

        var (raw, parseError) = await CallAndParseAsync(client, prompt, ct);
        if (raw is null)
        {
            yield return new GenerationEvent.Error(
                parseError ?? "Generation failed.", Retryable: true);
            yield break;
        }

        var draftQuestions = raw.Questions
            .Select((q, i) => ToDraft(q, i))
            .ToList();

        // Fact-check synchronously before we emit so questions arrive with
        // their final flags.
        if (request.RunFactCheck && draftQuestions.Count > 0)
        {
            yield return new GenerationEvent.Status("fact-checking");
            string? factCheckError = null;
            try
            {
                var checkedDraft = await factChecker.CheckAsync(
                    new DraftQuiz(
                        request.Title, "Generated", request.Provider, request.Model,
                        request.Topics, SourceText: null, draftQuestions),
                    providerKind, request.Model, ct);
                draftQuestions = [.. checkedDraft.Questions];
            }
            catch (Exception ex) { factCheckError = ex.Message; }

            if (factCheckError is not null)
            {
                yield return new GenerationEvent.Warning(
                    $"Fact-check skipped due to error: {factCheckError}");
            }
        }

        yield return new GenerationEvent.Status("finalising");

        foreach (var dq in draftQuestions)
        {
            yield return new GenerationEvent.Question(dq);
        }

        // Partial-count warnings (e.g. asked for 5 on The Office, got 3).
        var byTopic = draftQuestions
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

        var draft = new DraftQuiz(
            request.Title, "Generated", request.Provider, request.Model,
            request.Topics, SourceText: null, draftQuestions);

        yield return new GenerationEvent.Done(draft);
    }

    /// <summary>
    /// Calls the model and parses the JSON. On parse failure, retries once.
    /// </summary>
    internal static async Task<(RawQuizResponse? Raw, string? Error)> CallAndParseAsync(
        IChatClient client, string prompt, CancellationToken ct)
    {
        string? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            ChatResponse response;
            try
            {
                response = await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, prompt)],
                    new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
                    ct);
            }
            catch (Exception ex)
            {
                lastError = $"AI provider call failed: {ex.Message}";
                continue;
            }

            var text = ExtractJson(response.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                lastError = "AI returned empty response.";
                continue;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<RawQuizResponse>(text, JsonOpts);
                if (parsed is { Questions: not null }) return (parsed, null);
                lastError = "AI response missing 'questions' array.";
            }
            catch (Exception ex)
            {
                lastError = $"Failed to parse AI response as JSON: {ex.Message}";
            }
        }
        return (null, lastError);
    }

    /// <summary>
    /// Defensive: some models wrap JSON in ```json ... ``` despite the prompt.
    /// Slice from the first '{' to the last '}' to give parsing a fair shot.
    /// </summary>
    private static string ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text[start..(end + 1)] : text;
    }

    internal static DraftQuestion ToDraft(RawQuestion raw, int order) => new(
        Topic:            raw.Topic ?? string.Empty,
        Text:             raw.Text,
        Type:             NormaliseType(raw.Type),
        CorrectAnswer:    raw.CorrectAnswer,
        Options:          raw.Options,
        Explanation:      raw.Explanation,
        Order:            order,
        FactCheckFlagged: false,
        FactCheckNote:    null);

    private static string NormaliseType(string raw) => raw?.Trim() switch
    {
        "MultipleChoice" or "multiple_choice" or "mc" => "MultipleChoice",
        _                                              => "FreeText",
    };
}
