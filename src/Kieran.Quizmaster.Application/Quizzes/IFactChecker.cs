using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IFactChecker
{
    /// <summary>
    /// Sanity-checks every question in the draft, marking dubious ones with
    /// <see cref="DraftQuestion.FactCheckFlagged"/> = true and an explanation.
    /// Returns a new draft with the same shape — never mutates the input.
    /// </summary>
    Task<DraftQuiz> CheckAsync(
        Guid              userId,
        DraftQuiz         draft,
        AiProviderKind    provider,
        string            model,
        CancellationToken cancellationToken);
}
