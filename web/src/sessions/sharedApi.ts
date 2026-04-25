// Public, no-auth wrapper for the share endpoint. Intentionally NOT
// using apiFetch / Authorization-bearer headers — anyone with the
// share URL must be able to fetch the summary without an account.

export interface PublicQuestion {
  text: string;
  type: "MultipleChoice" | "FreeText";
  teamAnswer: string;
  correctAnswer: string;
  isCorrect: boolean;
  pointsAwarded: number;
  order: number;
}

export interface TopicChip {
  name: string;
  count: number;
}

export interface PublicSessionSummary {
  quizTitle: string;
  completedAt: string;
  score: number;
  maxScore: number;
  topics: TopicChip[];
  questions: PublicQuestion[];
}

export async function getPublicSession(token: string): Promise<PublicSessionSummary | null> {
  const res = await fetch(`/api/share/${encodeURIComponent(token)}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Share lookup failed: ${res.status}`);
  return res.json();
}
