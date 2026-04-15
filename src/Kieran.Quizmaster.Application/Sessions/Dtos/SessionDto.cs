namespace Kieran.Quizmaster.Application.Sessions.Dtos;

public sealed record SessionDto(
    Guid Id,
    Guid QuizId,
    string QuizTitle,
    /// <summary>"InProgress" | "AwaitingReveal" | "Graded"</summary>
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string PublicShareToken,
    IReadOnlyList<SessionQuestionDto> Questions,
    decimal Score,
    decimal MaxScore);
