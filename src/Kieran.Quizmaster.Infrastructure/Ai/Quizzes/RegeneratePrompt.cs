using System.Text;
using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

internal static class RegeneratePrompt
{
    public static string Build(
        string topic,
        string questionType,
        IReadOnlyList<QuestionDto> otherQuestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate ONE replacement quiz question on the topic: {topic}");
        sb.AppendLine($"Question type: {questionType} (preserve this — \"MultipleChoice\" or \"FreeText\")");
        sb.AppendLine();
        sb.AppendLine("AVOID duplicating any of these existing questions on the same quiz:");
        foreach (var q in otherQuestions)
        {
            sb.AppendLine($"- {q.Text} (answer: {q.CorrectAnswer})");
        }
        sb.AppendLine();
        sb.AppendLine("Return ONLY a JSON object matching this schema. No markdown, no commentary:");
        sb.AppendLine("""
            {
              "questions": [
                {
                  "topic": "<topic>",
                  "text": "<question text>",
                  "type": "MultipleChoice" | "FreeText",
                  "correctAnswer": "<answer>",
                  "options": ["<a>","<b>","<c>","<d>"],
                  "explanation": "<one short sentence or null>"
                }
              ]
            }
            """);
        return sb.ToString();
    }
}
