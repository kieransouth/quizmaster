using System.Runtime.CompilerServices;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

public sealed class QuizImporter(IAiChatClientFactory factory) : IQuizImporter
{
    public async IAsyncEnumerable<GenerationEvent> ImportAsync(
        Guid              userId,
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
        try   { client = await factory.CreateAsync(userId, providerKind, request.Model, ct); }
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

        var draft = new DraftQuiz(
            request.Title, "Imported", request.Provider, request.Model,
            Topics: [], request.SourceText, collected);

        yield return new GenerationEvent.Done(draft);
    }
}
