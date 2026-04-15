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
}

export interface ImportQuizRequest {
  title: string;
  sourceText: string;
  runFactCheck: boolean;
  provider: string;
  model: string;
}
