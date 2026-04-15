using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IQuizService
{
    /// <summary>Persists a draft as a new Quiz owned by the given user. Returns the new Quiz Id.</summary>
    Task<Guid> SaveAsync(DraftQuiz draft, Guid userId, CancellationToken ct);

    /// <summary>List the user's quizzes, newest first. For the dashboard.</summary>
    Task<IReadOnlyList<QuizSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns the full quiz if it exists AND is owned by the user, otherwise null.</summary>
    Task<QuizDetailDto?> GetByIdAsync(Guid quizId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Replaces the quiz's title + questions. Updates existing questions in place
    /// (matched by Id) and deletes any not in the request. Returns true on success,
    /// false if not found or not owned by the user.
    /// </summary>
    Task<bool> UpdateAsync(Guid quizId, Guid userId, UpdateQuizRequest request, CancellationToken ct);

    /// <summary>Deletes a quiz the user owns. Returns false if not found / not owned.</summary>
    Task<bool> DeleteAsync(Guid quizId, Guid userId, CancellationToken ct);
}
