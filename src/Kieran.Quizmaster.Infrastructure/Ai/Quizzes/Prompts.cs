using System.Text;
using Kieran.Quizmaster.Application.Quizzes.Dtos;

namespace Kieran.Quizmaster.Infrastructure.Ai.Quizzes;

internal static class Prompts
{
    public static string Generation(IReadOnlyList<TopicRequest> topics, double mcFraction)
    {
        var totalCount = topics.Sum(t => t.Count);
        var mcPct      = (int)Math.Round(mcFraction * 100);
        var ftPct      = 100 - mcPct;

        var topicLines = string.Join("\n",
            topics.Select(t => $"- {t.Name}: {t.Count} question{(t.Count == 1 ? "" : "s")}"));

        return $$"""
            You are creating a pub-style quiz. Generate exactly the requested number of questions for each topic.

            Topics:
            {{topicLines}}

            Total: {{totalCount}} questions.
            Question type mix: {{mcPct}}% multiple choice, {{ftPct}}% free text.

            Rules:
            - Multiple-choice questions MUST include exactly 4 options, one of which exactly matches "correctAnswer".
            - Free-text answers should be a short, unambiguous string (a name, a year, one or two words).
            - Set "topic" to the EXACT topic name from the list above.
            - Vary difficulty and avoid duplicates within a topic.
            - "explanation" is optional but encouraged — one short sentence.
            - Do NOT include numbering or "Q1:" prefixes in "text".

            JSON formatting (this is strict — output must parse with JSON.parse):
            - Every string value must be valid JSON. Escape any double quote inside a string as \" (e.g. What does \"DNS\" stand for?).
            - Escape backslashes as \\ and replace newlines inside strings with \n.
            - Do NOT use smart quotes (“ ” ‘ ’) as the outer string delimiters — only straight ASCII " is valid JSON.
            - No trailing commas. No comments.

            Return ONLY a JSON object matching this schema. No markdown, no commentary:
            {
              "questions": [
                {
                  "topic": "<topic name from list>",
                  "text": "<question text>",
                  "type": "MultipleChoice" | "FreeText",
                  "correctAnswer": "<the correct answer>",
                  "options": ["<opt1>", "<opt2>", "<opt3>", "<opt4>"],
                  "explanation": "<one short sentence or null>"
                }
              ]
            }
            """;
    }

    public static string Import(string sourceText) => $$"""
        Below is a quiz from another source. Extract every question and its correct answer
        into the JSON format described.

        - If the source shows multiple choice options, set "type" to "MultipleChoice" and include "options".
        - Otherwise set "type" to "FreeText" and omit "options".
        - "topic" is optional for imports — set to "" if there isn't a clear topic per question.
        - Preserve the order in which the questions appear in the source.

        Return ONLY a JSON object matching this schema. No markdown, no commentary:
        {
          "questions": [
            {
              "topic": "",
              "text": "<question text>",
              "type": "MultipleChoice" | "FreeText",
              "correctAnswer": "<the correct answer>",
              "options": ["<opt1>", "<opt2>", "<opt3>", "<opt4>"],
              "explanation": null
            }
          ]
        }

        Source:
        ===
        {{sourceText}}
        ===
        """;

    public static string FactCheck(IReadOnlyList<DraftQuestion> questions)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            sb.AppendLine($"{i}. {q.Text}");
            sb.AppendLine($"   Answer: {q.CorrectAnswer}");
        }

        return $$"""
            For each question below, decide whether the stated answer is factually correct.
            If you are uncertain or believe it is wrong, set "factuallyCorrect" to false and
            briefly explain in "note". If it is clearly correct, set "factuallyCorrect" to true
            and set "note" to null.

            Return ONLY a JSON object matching this schema:
            {
              "checks": [
                { "questionIndex": <int>, "factuallyCorrect": <bool>, "note": <string or null> }
              ]
            }

            Questions (zero-indexed):
            {{sb}}
            """;
    }
}
