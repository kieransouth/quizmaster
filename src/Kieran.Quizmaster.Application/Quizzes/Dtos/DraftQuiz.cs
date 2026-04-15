namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record DraftQuiz(
    string Title,
    /// <summary>"Generated" or "Imported".</summary>
    string Source,
    string ProviderUsed,
    string ModelUsed,
    /// <summary>Per-topic counts requested. Empty for imported.</summary>
    IReadOnlyList<TopicRequest> Topics,
    /// <summary>Original pasted text. Null for generated.</summary>
    string? SourceText,
    IReadOnlyList<DraftQuestion> Questions);
