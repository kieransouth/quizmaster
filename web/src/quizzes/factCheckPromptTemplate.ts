import type { DraftQuestion } from "./types";

/**
 * Mirrors the backend's Prompts.FactCheck template so the BYO-AI
 * fact-check tab can show the user the prompt they should paste into
 * any external AI tool. The frontend regenerates this on the fly as
 * questions change; the user copies it once and pastes the JSON
 * response back into Quizmaster.
 */
export function buildFactCheckPrompt(questions: DraftQuestion[]): string {
  const lines = questions
    .map((q, i) => `${i}. ${q.text}\n   Answer: ${q.correctAnswer}`)
    .join("\n");

  return `For each question below, decide whether the stated answer is factually correct.
If you are uncertain or believe it is wrong, set "factuallyCorrect" to false and
briefly explain in "note". If it is clearly correct, set "factuallyCorrect" to true
and set "note" to null.

Return ONLY a JSON object matching this schema:
{
  "checks": [
    { "questionIndex": <int>, "factuallyCorrect": <bool>, "note": <string or null> }
  ]
}

JSON formatting (this is strict — output must parse with JSON.parse):
- Every string value must be valid JSON. Escape any double quote inside a string as \\" (e.g. What does \\"DNS\\" stand for?).
- Escape backslashes as \\\\ and replace newlines inside strings with \\n.
- Do NOT use smart quotes (“ ” ‘ ’) as the outer string delimiters — only straight ASCII " is valid JSON.
- No trailing commas. No comments.

Questions (zero-indexed):
${lines}`;
}
