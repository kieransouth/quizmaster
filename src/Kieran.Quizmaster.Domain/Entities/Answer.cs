namespace Kieran.Quizmaster.Domain.Entities;

/// <summary>
/// One team answer per question per session. Quizmaster is a team game —
/// the host types in the team's agreed answer; there are no per-participant
/// rows.
/// </summary>
public class Answer
{
    public Guid Id { get; set; }

    public Guid QuizSessionId { get; set; }
    public QuizSession? QuizSession { get; set; }

    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    public string AnswerText { get; set; } = string.Empty;

    /// <summary>
    /// Null until graded. MC auto-grades on reveal; FreeText is set by the
    /// host on the grading screen.
    /// </summary>
    public bool? IsCorrect { get; set; }

    /// <summary>
    /// 0.0–1.0. MC flips to 0 or 1 on reveal; FreeText accepts any decimal
    /// in that range so the host can award partial "close enough" credit.
    /// </summary>
    public decimal PointsAwarded { get; set; }

    public string? GradingNote { get; set; }
}
