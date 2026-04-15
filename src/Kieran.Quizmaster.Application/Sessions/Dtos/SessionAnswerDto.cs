namespace Kieran.Quizmaster.Application.Sessions.Dtos;

public sealed record SessionAnswerDto(
    string AnswerText,
    bool? IsCorrect,
    decimal PointsAwarded,
    string? GradingNote);
