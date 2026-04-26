using System.Text.Json;
using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

/// <summary>
/// Single source of truth for applying a <c>RawFactCheckResponse</c> JSON
/// shape onto a draft question list. Used by both the AI fact-check
/// (<see cref="FactChecker"/> after a model call) and the BYO fact-check
/// (user-pasted JSON, no AI call). Out-of-range or missing indices are
/// silently skipped — same generous failure mode the in-stream AI flow
/// already used.
/// </summary>
public static class FactCheckJsonMerger
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses <paramref name="sourceJson"/> against the
    /// <c>{ checks: [{ questionIndex, factuallyCorrect, note }] }</c>
    /// schema and returns a new question list with flag fields applied.
    /// Throws <see cref="InvalidOperationException"/> with a useful
    /// message on malformed JSON or when the <c>checks</c> array is
    /// missing.
    /// </summary>
    public static IReadOnlyList<DraftQuestion> Apply(
        IReadOnlyList<DraftQuestion> questions,
        string                       sourceJson)
    {
        if (string.IsNullOrWhiteSpace(sourceJson))
            throw new InvalidOperationException(
                "Fact-check JSON is empty — paste the AI's response into the box.");

        var text = QuizJsonImporter.ExtractJson(sourceJson);

        RawFactCheckResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawFactCheckResponse>(text, JsonOpts);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Couldn't parse fact-check JSON: {ex.Message}");
        }

        if (parsed is null || parsed.Checks is null)
            throw new InvalidOperationException(
                "Fact-check JSON didn't contain a 'checks' array — the model probably ignored the schema.");

        return Merge(questions, parsed.Checks);
    }

    /// <summary>
    /// Internal entry used by <see cref="FactChecker"/> after it parses
    /// the AI response itself. Same merge semantics as <see cref="Apply"/>.
    /// </summary>
    internal static IReadOnlyList<DraftQuestion> Merge(
        IReadOnlyList<DraftQuestion> questions,
        IReadOnlyList<RawFactCheck>  checks)
    {
        if (checks.Count == 0) return questions;

        // Last-writer-wins on duplicate indices, matches the previous behaviour.
        var byIndex = new Dictionary<int, RawFactCheck>(checks.Count);
        foreach (var c in checks) byIndex[c.QuestionIndex] = c;

        return questions.Select((q, i) =>
        {
            if (!byIndex.TryGetValue(i, out var check)) return q;
            var flagged = !check.FactuallyCorrect;
            return q with
            {
                FactCheckFlagged = flagged,
                FactCheckNote    = flagged ? (check.Note ?? "Model flagged this answer as suspect.") : null,
            };
        }).ToList();
    }
}
