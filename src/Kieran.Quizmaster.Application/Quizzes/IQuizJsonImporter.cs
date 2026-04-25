using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Application.Quizzes;

public interface IQuizJsonImporter
{
    /// <summary>
    /// Parses pre-formed JSON (typically produced by an external AI tool
    /// using the prompt template Quizmaster recommends) and emits the same
    /// event vocabulary as the AI-driven import flows so the existing
    /// frontend stream consumer renders the result identically.
    /// </summary>
    IAsyncEnumerable<GenerationEvent> ImportFromJsonAsync(
        ImportFromJsonRequest request,
        CancellationToken     cancellationToken);
}
