using System.Text.Json;
using Kieran.Quizmaster.Application.Ai;
using Kieran.Quizmaster.Application.Quizzes;
using Kieran.Quizmaster.Application.Quizzes.Dtos;
using Kieran.Quizmaster.Domain.Enumerations;
using Kieran.Quizmaster.Infrastructure.Ai.Quizzes;
using Kieran.Quizmaster.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace Kieran.Quizmaster.Infrastructure.Quizzes;

public sealed class QuizQuestionRegenerator(
    ApplicationDbContext db,
    IAiChatClientFactory factory) : IQuizQuestionRegenerator
{
    public async Task<QuestionDto?> RegenerateAsync(
        Guid quizId,
        Guid questionId,
        Guid userId,
        RegenerateQuestionRequest request,
        CancellationToken ct)
    {
        var quiz = await db.Quizzes
            .Include(q => q.Questions)
            .ThenInclude(qu => qu.Topic)
            .FirstOrDefaultAsync(q => q.Id == quizId && q.CreatedByUserId == userId, ct);
        if (quiz is null) return null;

        var target = quiz.Questions.FirstOrDefault(q => q.Id == questionId);
        if (target is null) return null;

        if (!AiProviderKind.TryFromName(request.Provider, out var providerKind))
            throw new InvalidOperationException($"Unknown provider '{request.Provider}'.");

        var client = factory.Create(providerKind, request.Model);

        var others = quiz.Questions
            .Where(q => q.Id != questionId)
            .OrderBy(q => q.Order)
            .Select(q => QuizService.ToQuestionDto(q))
            .ToList();

        var prompt = RegeneratePrompt.Build(
            topic: target.Topic?.Name ?? string.Empty,
            questionType: target.Type.Name,
            otherQuestions: others);

        // Stream just to reuse the parser; we expect exactly one question.
        await foreach (var evt in QuizGenerator.StreamQuestionsAsync(client, prompt, ct))
        {
            if (evt is GenerationEvent.Question q)
            {
                // Don't persist — the controller returns the new question and
                // the user saves the whole quiz when they're happy with it.
                return new QuestionDto(
                    Id:               questionId, // keep existing id so PUT can update in place
                    Topic:            q.Item.Topic,
                    Text:             q.Item.Text,
                    Type:             q.Item.Type,
                    CorrectAnswer:    q.Item.CorrectAnswer,
                    Options:          q.Item.Options,
                    Explanation:      q.Item.Explanation,
                    Order:            target.Order,
                    FactCheckFlagged: false,
                    FactCheckNote:    null);
            }
            if (evt is GenerationEvent.Error e)
            {
                throw new InvalidOperationException(e.Message);
            }
        }

        throw new InvalidOperationException("Regeneration produced no question.");
    }
}
