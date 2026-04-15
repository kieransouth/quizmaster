namespace Kieran.Quizmaster.Application.Quizzes.Dtos;

/// <summary>Slim shape used by the dashboard list.</summary>
public sealed record QuizSummaryDto(
    Guid Id,
    string Title,
    string Source,
    string ProviderUsed,
    string ModelUsed,
    DateTimeOffset CreatedAt,
    int QuestionCount);
