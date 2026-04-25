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

        var prompt    = Prompts.Import(request.SourceText);
        var collected = new List<DraftQuestion>();

        await foreach (var evt in QuizGenerator.StreamQuestionsAsync(client, prompt, ct))
        {
            if (evt is GenerationEvent.Question q) collected.Add(q.Item);
            yield return evt;
            if (evt is GenerationEvent.Error) yield break;
        }

        if (collected.Count == 0)
        {
            yield return new GenerationEvent.Warning("No questions extracted from the source text.");
        }

        if (request.RunFactCheck && collected.Count > 0)
        {
            yield return new GenerationEvent.Status("fact-checking");
            var factCheckProviderName = request.FactCheckProvider ?? request.Provider;
            var factCheckModel        = request.FactCheckModel    ?? request.Model;
            string?              factCheckError = null;
            List<DraftQuestion>? updated        = null;

            if (!AiProviderKind.TryFromName(factCheckProviderName, out var factCheckKind))
            {
                factCheckError = $"unknown provider '{factCheckProviderName}'";
            }
            else
            {
                try
                {
                    var checkedDraft = await factChecker.CheckAsync(
                        new DraftQuiz(
                            request.Title, "Imported", request.Provider, request.Model,
                            Topics: [], request.SourceText, collected),
                        factCheckKind, factCheckModel, ct);
                    updated = [.. checkedDraft.Questions];
                }
                catch (Exception ex) { factCheckError = ex.Message; }
            }

            if (factCheckError is not null)
            {
                yield return new GenerationEvent.Warning(
                    $"Fact-check skipped due to error: {factCheckError}");
            }
            else if (updated is not null)
            {
                collected = updated;
                foreach (var q in collected.Where(q => q.FactCheckFlagged))
                {
                    yield return new GenerationEvent.Question(q);
                }
            }
        }

        var draft = new DraftQuiz(
            request.Title, "Imported", request.Provider, request.Model,
            Topics: [], request.SourceText, collected);

        yield return new GenerationEvent.Done(draft);
    }
}
