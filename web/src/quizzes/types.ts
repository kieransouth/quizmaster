export interface TopicRequest {
  name: string;
  count: number;
}

export interface DraftQuestion {
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

export interface DraftQuiz {
  title: string;
  source: "Generated" | "Imported";
  providerUsed: string;
  modelUsed: string;
  topics: TopicRequest[];
  sourceText?: string | null;
  questions: DraftQuestion[];
}

// Polymorphic events from the SSE stream
export type GenerationEvent =
  | { type: "status";   stage: string }
  | { type: "question"; item: DraftQuestion }
  | { type: "warning";  message: string }
  | { type: "done";     quiz: DraftQuiz }
  | { type: "error";    message: string; retryable: boolean };

export interface GenerateQuizRequest {
  title: string;
  topics: TopicRequest[];
  multipleChoiceFraction: number;
  runFactCheck: boolean;
  provider: string;
  model: string;
  /** Optional: provider used for the fact-check pass. Falls back to `provider`. */
  factCheckProvider?: string;
  /** Optional: model used for the fact-check pass. Falls back to `model`. */
  factCheckModel?: string;
}

export interface ImportQuizRequest {
  title: string;
  sourceText: string;
  runFactCheck: boolean;
  provider: string;
  model: string;
  factCheckProvider?: string;
  factCheckModel?: string;
}

/** "Bring your own AI" — pre-formed JSON the user got from an external tool. */
export interface ImportFromJsonRequest {
  title: string;
  topics: TopicRequest[];
  sourceJson: string;
}
