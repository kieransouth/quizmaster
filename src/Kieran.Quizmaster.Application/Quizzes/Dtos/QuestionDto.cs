namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>A persisted question — same shape as <see cref="DraftQuestion"/> plus an Id.</summary>
public sealed record QuestionDto(
    Guid Id,
    string Topic,
    string Text,
    string Type,
    string CorrectAnswer,
    IReadOnlyList<string>? Options,
    string? Explanation,
    int Order,
    bool FactCheckFlagged,
    string? FactCheckNote);
