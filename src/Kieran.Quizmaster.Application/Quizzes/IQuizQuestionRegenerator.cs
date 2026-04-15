using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IQuizQuestionRegenerator
{
    /// <summary>
    /// Generates a single replacement question for the given quiz/question.
    /// The other questions in the quiz are passed to the model so it knows
    /// what NOT to duplicate.
    /// </summary>
    /// <returns>The new question on success, or null when the quiz/question
    /// isn't found / not owned by the user.</returns>
    Task<QuestionDto?> RegenerateAsync(
        Guid quizId,
        Guid questionId,
        Guid userId,
        RegenerateQuestionRequest request,
        CancellationToken ct);
}
