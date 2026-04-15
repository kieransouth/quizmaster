using System.Runtime.CompilerServices;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

public sealed class QuizImporter(
    IAiChatClientFactory factory,
    IFactChecker         factChecker) : IQuizImporter
{
    public async IAsyncEnumerable<GenerationEvent> ImportAsync(
        ImportQuizRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new GenerationEvent.Status("importing");

        if (!AiProviderKind.TryFromName(request.Provider, out var providerKind))
        {
            yield return new GenerationEvent.Error(
                $"Unknown provider '{request.Provider}'.", Retryable: false);
            yield break;
        }

        IChatClient? client = null;
        string?      createError = null;
        try   { client = factory.Create(providerKind, request.Model); }
        catch (Exception ex) { createError = ex.Message; }
        if (client is null)
        {
            yield return new GenerationEvent.Error(createError ?? "Failed to create AI client.", Retryable: false);
            yield break;
        }

        var prompt = Prompts.Import(request.SourceText);
        var (raw, error) = await QuizGenerator.CallAndParseAsync(client, prompt, ct);
        if (raw is null)
        {
            yield return new GenerationEvent.Error(error ?? "Import failed.", Retryable: true);
            yield break;
        }

        var draftQuestions = raw.Questions
            .Select((q, i) => QuizGenerator.ToDraft(q, i))
            .ToList();

        if (draftQuestions.Count == 0)
        {
            yield return new GenerationEvent.Warning("No questions extracted from the source text.");
        }

        if (request.RunFactCheck && draftQuestions.Count > 0)
        {
            yield return new GenerationEvent.Status("fact-checking");
            string? factCheckError = null;
            try
            {
                var checkedDraft = await factChecker.CheckAsync(
                    new DraftQuiz(
                        request.Title, "Imported", request.Provider, request.Model,
                        Topics: [], request.SourceText, draftQuestions),
                    providerKind, request.Model, ct);
                draftQuestions = [.. checkedDraft.Questions];
            }
            catch (Exception ex) { factCheckError = ex.Message; }

            if (factCheckError is not null)
            {
                yield return new GenerationEvent.Warning(
                    $"Fact-check skipped due to error: {factCheckError}");
            }
        }

        yield return new GenerationEvent.Status("finalising");

        foreach (var dq in draftQuestions)
        {
            yield return new GenerationEvent.Question(dq);
        }

        var draft = new DraftQuiz(
            request.Title, "Imported", request.Provider, request.Model,
            Topics: [], request.SourceText, draftQuestions);

        yield return new GenerationEvent.Done(draft);
    }
}
