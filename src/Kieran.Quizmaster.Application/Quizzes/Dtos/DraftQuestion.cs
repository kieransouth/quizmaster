namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

public sealed record DraftQuestion(
    /// <summary>Topic this question came from. Empty for imported quizzes.</summary>
    string Topic,
    string Text,
    /// <summary>"MultipleChoice" or "FreeText".</summary>
    string Type,
    string CorrectAnswer,
    /// <summary>Options for multiple choice (typically 4). Null for free-text.</summary>
    IReadOnlyList<string>? Options,
    string? Explanation,
    int Order,
    bool FactCheckFlagged,
    string? FactCheckNote);
