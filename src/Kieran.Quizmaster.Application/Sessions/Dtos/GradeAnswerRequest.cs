namespace Kieran.Quizmaster.Application.Sessions.Dtos;

/// <summary>Manual grading (free-text questions). PointsAwarded between 0.0 and 1.0.</summary>
public sealed record GradeAnswerRequest(
    bool IsCorrect,
    decimal PointsAwarded,
    string? GradingNote);
