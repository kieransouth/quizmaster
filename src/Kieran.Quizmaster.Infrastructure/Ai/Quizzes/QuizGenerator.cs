using System.Runtime.CompilerServices;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

public sealed class QuizGenerator(IAiChatClientFactory factory) : IQuizGenerator
{
    public async IAsyncEnumerable<GenerationEvent> GenerateAsync(
        Guid                 userId,
        GenerateQuizRequest  request,
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
        try   { client = await factory.CreateAsync(userId, providerKind, request.Model, ct); }
        catch (Exception ex) { createError = ex.Message; }
        if (client is null)
        {
            yield return new GenerationEvent.Error(createError ?? "Failed to create AI client.", Retryable: false);
            yield break;
        }

        var prompt = Prompts.Generation(request.Topics, request.MultipleChoiceFraction);

        // Stream the model output and emit each question as it completes,
        // so the user sees them appear progressively rather than all at once
        // after a long wait.
        var collected = new List<DraftQuestion>();
        await foreach (var evt in StreamQuestionsAsync(client, prompt, ct))
        {
            if (evt is GenerationEvent.Question q)
            {
                collected.Add(q.Item);
            }
            yield return evt;
            if (evt is GenerationEvent.Error) yield break;
        }

        if (collected.Count == 0)
        {
            yield return new GenerationEvent.Error(
                "Generation produced no questions.", Retryable: true);
            yield break;
        }

        // Partial-count warnings (e.g. asked for 5 on The Office, got 3).
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

        // Interleave across topics so the host doesn't get all 5 of one topic
        // back-to-back. Only kicks in when there are 2+ distinct topics —
        // single-topic generations have nothing to interleave.
        var interleaved = InterleaveByTopic(collected);

        var draft = new DraftQuiz(
            request.Title, "Generated", request.Provider, request.Model,
            request.Topics, SourceText: null, interleaved);

        yield return new GenerationEvent.Done(draft);
    }

    /// <summary>
    /// Round-robin pull from each topic group with a single Fisher-Yates
    /// shuffle of the cycle order, so consecutive questions are unlikely
    /// to share a topic and the order varies between runs. Order values are
    /// re-assigned to match the new sequence. No-op when fewer than 2
    /// distinct topics are present.
    /// </summary>
    public static IReadOnlyList<DraftQuestion> InterleaveByTopic(IReadOnlyList<DraftQuestion> questions)
    {
        var groups = questions
            .GroupBy(q => q.Topic, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Queue<DraftQuestion>(g))
            .ToList();
        if (groups.Count < 2) return questions;

        var rng = Random.Shared;
        // Shuffle cycle order once so the lead topic varies between runs.
        for (var i = groups.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (groups[i], groups[j]) = (groups[j], groups[i]);
        }

        var result = new List<DraftQuestion>(questions.Count);
        while (groups.Any(g => g.Count > 0))
        {
            foreach (var g in groups)
            {
                if (g.Count > 0) result.Add(g.Dequeue());
            }
        }

        // Re-assign Order so the saved/displayed sequence matches the new layout.
        return result
            .Select((q, i) => q with { Order = i })
            .ToList();
    }

    /// <summary>
    /// Streams from <see cref="IChatClient.GetStreamingResponseAsync"/> and
    /// emits per-question events as soon as their JSON object is complete.
    /// </summary>
    internal static async IAsyncEnumerable<GenerationEvent> StreamQuestionsAsync(
        IChatClient client,
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        IAsyncEnumerable<ChatResponseUpdate>? stream = null;
        string? streamError = null;
        try
        {
            stream = client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
                ct);
        }
        catch (Exception ex) { streamError = $"AI provider call failed: {ex.Message}"; }

        if (streamError is not null || stream is null)
        {
            yield return new GenerationEvent.Error(streamError ?? "Failed to start stream.", Retryable: true);
            yield break;
        }

        var parser = new StreamingQuestionParser();
        var order  = 0;
        string? iterationError = null;

        var enumerator = stream.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool moved;
                try { moved = await enumerator.MoveNextAsync(); }
                catch (Exception ex) { iterationError = $"AI stream failed: {ex.Message}"; break; }

                if (!moved) break;

                var update = enumerator.Current;
                var text = update.Text;
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var raw in parser.Append(text))
                {
                    yield return new GenerationEvent.Question(ToDraft(raw, order++));
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (iterationError is not null)
        {
            yield return new GenerationEvent.Error(iterationError, Retryable: true);
        }
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
