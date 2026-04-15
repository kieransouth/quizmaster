import { apiFetch } from "../api/client";

export type SessionStatus = "InProgress" | "AwaitingReveal" | "Graded";

export interface SessionAnswerDto {
  answerText: string;
  isCorrect: boolean | null;
  pointsAwarded: number;
  gradingNote: string | null;
}

export interface SessionQuestionDto {
  id: string;
  text: string;
  type: "MultipleChoice" | "FreeText";
  /** Null while session is InProgress. */
  correctAnswer: string | null;
  options: string[] | null;
  explanation: string | null;
  order: number;
  answer: SessionAnswerDto;
}

export interface SessionDto {
  id: string;
  quizId: string;
  quizTitle: string;
  status: SessionStatus;
  startedAt: string;
  completedAt: string | null;
  publicShareToken: string;
  questions: SessionQuestionDto[];
  score: number;
  maxScore: number;
}

export async function startSession(quizId: string): Promise<SessionDto> {
  const res = await apiFetch("/api/sessions", {
    method: "POST",
    body: JSON.stringify({ quizId }),
  });
  if (!res.ok) throw new Error(`Start failed: ${res.status}`);
  return res.json();
}

export async function getSession(id: string): Promise<SessionDto | null> {
  const res = await apiFetch(`/api/sessions/${id}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Get failed: ${res.status}`);
  return res.json();
}

export async function recordAnswer(
  sessionId: string,
  questionId: string,
  answerText: string,
): Promise<SessionDto> {
  const res = await apiFetch(`/api/sessions/${sessionId}/answers/${questionId}`, {
    method: "PUT",
    body: JSON.stringify({ answerText }),
  });
  if (!res.ok) throw await asError(res, "Record answer failed");
  return res.json();
}

export async function reveal(sessionId: string): Promise<SessionDto> {
  const res = await apiFetch(`/api/sessions/${sessionId}/reveal`, { method: "POST" });
  if (!res.ok) throw await asError(res, "Reveal failed");
  return res.json();
}

export async function gradeAnswer(
  sessionId: string,
  questionId: string,
  isCorrect: boolean,
  pointsAwarded: number,
  gradingNote?: string,
): Promise<SessionDto> {
  const res = await apiFetch(`/api/sessions/${sessionId}/answers/${questionId}/grade`, {
    method: "PUT",
    body: JSON.stringify({ isCorrect, pointsAwarded, gradingNote }),
  });
  if (!res.ok) throw await asError(res, "Grade failed");
  return res.json();
}

export async function completeSession(sessionId: string): Promise<SessionDto> {
  const res = await apiFetch(`/api/sessions/${sessionId}/complete`, { method: "POST" });
  if (!res.ok) throw await asError(res, "Complete failed");
  return res.json();
}

async function asError(res: Response, fallback: string): Promise<Error> {
  const body = await res.json().catch(() => null);
  return new Error((body as { error?: string } | null)?.error ?? `${fallback}: ${res.status}`);
}
