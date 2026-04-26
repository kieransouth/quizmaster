using System.Text.Json;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

public sealed class FactChecker(IAiChatClientFactory factory) : IFactChecker
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<DraftQuiz> CheckAsync(
        Guid              userId,
        DraftQuiz         draft,
        AiProviderKind    provider,
        string            model,
        CancellationToken cancellationToken)
    {
        if (draft.Questions.Count == 0) return draft;

        var client   = await factory.CreateAsync(userId, provider, model, cancellationToken);
        var prompt   = Prompts.FactCheck(draft.Questions);
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
            cancellationToken);

        var text = ExtractJson(response.Text);
        RawFactCheckResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawFactCheckResponse>(text, JsonOpts);
        }
        catch
        {
            // Don't fail the run; the orchestrator surfaces this as a warning.
            return draft;
        }
        if (parsed is null || parsed.Checks.Count == 0) return draft;

        var byIndex = parsed.Checks.ToDictionary(c => c.QuestionIndex);

        var updated = draft.Questions.Select((q, i) =>
        {
            if (!byIndex.TryGetValue(i, out var check)) return q;
            var flagged = !check.FactuallyCorrect;
            return q with
            {
                FactCheckFlagged = flagged,
                FactCheckNote    = flagged ? (check.Note ?? "Model flagged this answer as suspect.") : null,
            };
        }).ToList();

        return draft with { Questions = updated };
    }

    private static string ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text[start..(end + 1)] : text;
    }
}
