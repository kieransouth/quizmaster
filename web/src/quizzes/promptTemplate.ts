import type { TopicRequest } from "./types";

/**
 * Mirrors the backend's Prompts.Generation template so the BYO-AI tab can
 * show the recommended prompt instantly as the user edits topics. Kept
 * deliberately close to the backend version — drift here just means the
 * frontend's "recommended prompt" is slightly off; the user can edit it
 * before pasting into their AI of choice.
 */
export function buildGenerationPrompt(
  topics: TopicRequest[],
  multipleChoiceFraction: number,
): string {
  const totalCount = topics.reduce((sum, t) => sum + (t.count || 0), 0);
  const mcPct = Math.round(multipleChoiceFraction * 100);
  const ftPct = 100 - mcPct;

  const topicLines = topics
    .filter((t) => t.name.trim())
    .map((t) => `- ${t.name}: ${t.count} question${t.count === 1 ? "" : "s"}`)
    .join("\n");

  return `You are creating a pub-style quiz. Generate exactly the requested number of questions for each topic.

Topics:
${topicLines || "(no topics yet — add some on the left)"}

Total: ${totalCount} questions.
Question type mix: ${mcPct}% multiple choice, ${ftPct}% free text.

Rules:
- Multiple-choice questions MUST include exactly 4 options, one of which exactly matches "correctAnswer".
- Free-text answers should be a short, unambiguous string (a name, a year, one or two words).
- Set "topic" to the EXACT topic name from the list above.
- Vary difficulty and avoid duplicates within a topic.
- "explanation" is optional but encouraged — one short sentence.
- Do NOT include numbering or "Q1:" prefixes in "text".

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
}`;
}
