using Kieran.Quizmaster.Application.Sessions.Dtos;

namespace Kieran.Quizmaster.Application.Sessions;

public interface ISessionService
{
    /// <summary>
    /// Starts a new session for the given quiz. Pre-creates blank Answer
    /// rows for every question so subsequent record/grade calls are pure
    /// upserts. Returns the full session.
    /// </summary>
    Task<SessionDto?> StartAsync(Guid quizId, Guid hostUserId, CancellationToken ct);

    /// <summary>Returns the full session if owned by the user, otherwise null.</summary>
    Task<SessionDto?> GetByIdAsync(Guid sessionId, Guid hostUserId, CancellationToken ct);

    /// <summary>
    /// Idempotent upsert of the team's answer for one question. Allowed only
    /// while status is InProgress (back-navigation edits remain valid).
    /// </summary>
    Task<SessionResult> RecordAnswerAsync(
        Guid sessionId, Guid questionId, Guid hostUserId,
        RecordAnswerRequest request, CancellationToken ct);

    /// <summary>
    /// Flips status to AwaitingReveal and auto-grades MultipleChoice answers
    /// (exact match of AnswerText vs CorrectAnswer = 1 point, otherwise 0).
    /// FreeText answers are left ungraded for the host to mark.
    /// </summary>
    Task<SessionResult> RevealAsync(Guid sessionId, Guid hostUserId, CancellationToken ct);

    /// <summary>
    /// Manual grading for a single answer. Allowed only while status is
    /// AwaitingReveal. PointsAwarded is clamped to [0, 1].
    /// </summary>
    Task<SessionResult> GradeAnswerAsync(
        Guid sessionId, Guid questionId, Guid hostUserId,
        GradeAnswerRequest request, CancellationToken ct);

    /// <summary>
    /// Marks the session Graded and stamps CompletedAt. Rejects if any
    /// answer is still ungraded (IsCorrect == null).
    /// </summary>
    Task<SessionResult> CompleteAsync(Guid sessionId, Guid hostUserId, CancellationToken ct);
}

public abstract record SessionResult
{
    public sealed record Ok(SessionDto Session) : SessionResult;
    public sealed record NotFound : SessionResult;
    /// <summary>Operation isn't valid in the session's current state.</summary>
    public sealed record InvalidState(string Reason) : SessionResult;
}
