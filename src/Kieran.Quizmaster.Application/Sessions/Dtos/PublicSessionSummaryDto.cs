namespace Kieran.Quizmaster.Application.Sessions.Dtos;

/// <summary>
/// Public, no-auth view of a completed session served at /share/:token.
/// Deliberately excludes anything that could identify the host
/// (no UserId, no email, no DisplayName, no internal session Id) and
/// drops fields the public viewer doesn't need (raw provider/model,
/// share token).
/// </summary>
public sealed record PublicSessionSummaryDto(
    string QuizTitle,
    DateTimeOffset CompletedAt,
    decimal Score,
    decimal MaxScore,
    IReadOnlyList<TopicChip> Topics,
    IReadOnlyList<PublicQuestionDto> Questions);

public sealed record TopicChip(string Name, int Count);

public sealed record PublicQuestionDto(
    string Text,
    string Type,
    /// <summary>Team's submitted answer (may be empty if they skipped).</summary>
    string TeamAnswer,
    string CorrectAnswer,
    bool IsCorrect,
    decimal PointsAwarded,
    int Order);
