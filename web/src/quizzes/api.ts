import { apiFetch } from "../api/client";
import type { DraftQuestion, DraftQuiz } from "./types";

export interface QuestionDto {
  id: string;
  topic: string;
  text: string;
  type: "MultipleChoice" | "FreeText";
  correctAnswer: string;
  options?: string[] | null;
  explanation?: string | null;
  order: number;
  factCheckFlagged: boolean;
  factCheckNote?: string | null;
}

export interface QuizSummaryDto {
  id: string;
  title: string;
  source: "Generated" | "Imported";
  providerUsed: string;
  modelUsed: string;
  createdAt: string;
  questionCount: number;
}

export interface QuizDetailDto {
  id: string;
  title: string;
  source: "Generated" | "Imported";
  providerUsed: string;
  modelUsed: string;
  sourceText?: string | null;
  createdAt: string;
  topics: { name: string; count: number }[];
  questions: QuestionDto[];
}

export interface UpdateQuestionRequest {
  id: string;
  text: string;
  correctAnswer: string;
  options?: string[] | null;
  explanation?: string | null;
  order: number;
}

export interface UpdateQuizRequest {
  title: string;
  questions: UpdateQuestionRequest[];
}

export async function saveQuiz(draft: DraftQuiz): Promise<{ id: string }> {
  const res = await apiFetch("/api/quizzes", {
    method: "POST",
    body: JSON.stringify(draft),
  });
  if (!res.ok) throw new Error(`Save failed: ${res.status}`);
  return res.json();
}

export async function listMyQuizzes(): Promise<QuizSummaryDto[]> {
  const res = await apiFetch("/api/quizzes");
  if (!res.ok) throw new Error(`List failed: ${res.status}`);
  return res.json();
}

export async function getQuiz(id: string): Promise<QuizDetailDto | null> {
  const res = await apiFetch(`/api/quizzes/${id}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Get failed: ${res.status}`);
  return res.json();
}

export async function updateQuiz(id: string, body: UpdateQuizRequest): Promise<void> {
  const res = await apiFetch(`/api/quizzes/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`Update failed: ${res.status}`);
}

export async function deleteQuiz(id: string): Promise<void> {
  const res = await apiFetch(`/api/quizzes/${id}`, { method: "DELETE" });
  if (!res.ok) throw new Error(`Delete failed: ${res.status}`);
}

// ----- Fact-check (decoupled from generation) -----

async function readErrorMessage(res: Response, fallback: string): Promise<string> {
  const body = await res.json().catch(() => null);
  return (body?.error as string | undefined) ?? `${fallback}: ${res.status}`;
}

export async function factCheckDraftAi(
  questions: DraftQuestion[],
  provider: string,
  model: string,
): Promise<DraftQuestion[]> {
  const res = await apiFetch("/api/quizzes/fact-check", {
    method: "POST",
    body: JSON.stringify({ questions, provider, model }),
  });
  if (!res.ok) throw new Error(await readErrorMessage(res, "Fact-check failed"));
  const body = (await res.json()) as { questions: DraftQuestion[] };
  return body.questions;
}

export async function factCheckDraftJson(
  questions: DraftQuestion[],
  sourceJson: string,
): Promise<DraftQuestion[]> {
  const res = await apiFetch("/api/quizzes/fact-check-json", {
    method: "POST",
    body: JSON.stringify({ questions, sourceJson }),
  });
  if (!res.ok) throw new Error(await readErrorMessage(res, "Fact-check failed"));
  const body = (await res.json()) as { questions: DraftQuestion[] };
  return body.questions;
}

export async function factCheckSavedAi(
  quizId: string,
  provider: string,
  model: string,
): Promise<QuizDetailDto> {
  const res = await apiFetch(`/api/quizzes/${quizId}/fact-check`, {
    method: "POST",
    body: JSON.stringify({ provider, model }),
  });
  if (!res.ok) throw new Error(await readErrorMessage(res, "Fact-check failed"));
  return res.json();
}

export async function factCheckSavedJson(
  quizId: string,
  sourceJson: string,
): Promise<QuizDetailDto> {
  const res = await apiFetch(`/api/quizzes/${quizId}/fact-check-json`, {
    method: "POST",
    body: JSON.stringify({ sourceJson }),
  });
  if (!res.ok) throw new Error(await readErrorMessage(res, "Fact-check failed"));
  return res.json();
}

export async function regenerateQuestion(
  quizId: string,
  questionId: string,
  provider: string,
  model: string,
): Promise<QuestionDto> {
  const res = await apiFetch(`/api/quizzes/${quizId}/regenerate-question/${questionId}`, {
    method: "POST",
    body: JSON.stringify({ provider, model }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.error ?? `Regenerate failed: ${res.status}`);
  }
  return res.json();
}
