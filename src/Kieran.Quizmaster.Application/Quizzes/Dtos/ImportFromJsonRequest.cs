namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>
/// "Bring your own AI" import: the user generated questions in an
/// external tool (ChatGPT, Claude.ai, a model we don't expose, etc)
/// and is pasting back the JSON. No AI calls happen on our side.
/// </summary>
public sealed record ImportFromJsonRequest(
    string Title,
    /// <summary>Optional topic chips for the saved quiz (informational, no AI use).</summary>
    IReadOnlyList<TopicRequest> Topics,
    /// <summary>The raw JSON string the AI returned. May be wrapped in markdown fences.</summary>
    string SourceJson);
