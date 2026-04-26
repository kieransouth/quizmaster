import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useProviders } from "../ai/useProviders";
import {
  deleteQuiz,
  factCheckSavedAi,
  factCheckSavedJson,
  getQuiz,
  regenerateQuestion,
  updateQuiz,
  type QuestionDto,
  type QuizDetailDto,
  type UpdateQuestionRequest,
} from "../quizzes/api";
import { FactCheckPanel } from "../quizzes/FactCheckPanel";
import { startSession } from "../sessions/api";
import { ThemePicker } from "../ui/ThemePicker";
import { useToast } from "../ui/toast";

export default function QuizDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const providers = useProviders();
  const { push } = useToast();

  const [quiz, setQuiz] = useState<QuizDetailDto | null | "loading" | "notfound">("loading");
  const [title, setTitle] = useState("");
  const [questions, setQuestions] = useState<QuestionDto[]>([]);
  const [provider, setProvider] = useState("");
  const [model, setModel] = useState("");

  const [savingState, setSavingState] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [saveError, setSaveError] = useState<string | null>(null);
  // Hide answers by default so the host can browse a saved quiz without
  // spoiling themselves before play. Toggle to reveal when actually editing.
  const [showAnswers, setShowAnswers] = useState(false);

  const [showFactCheck, setShowFactCheck] = useState(false);
  const [factCheckBusy, setFactCheckBusy] = useState(false);
  const [factCheckError, setFactCheckError] = useState<string | null>(null);

  // Load
  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    (async () => {
      try {
        const data = await getQuiz(id);
        if (cancelled) return;
        if (!data) {
          setQuiz("notfound");
        } else {
          setQuiz(data);
          setTitle(data.title);
          setQuestions(data.questions);
          setProvider(data.providerUsed);
          setModel(data.modelUsed);
        }
      } catch {
        if (!cancelled) setQuiz("notfound");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  const dirty = useMemo(() => {
    if (typeof quiz !== "object" || quiz === null) return false;
    // `quiz` is narrowed to QuizDetailDto here — `loaded` is declared
    // later in the function body (after early returns) so we use the
    // narrowed `quiz` directly inside this memo.
    if (title !== quiz.title) return true;
    if (questions.length !== quiz.questions.length) return true;
    return questions.some((q, i) => {
      const orig = quiz.questions[i];
      if (!orig || orig.id !== q.id) return true;
      return (
        q.text !== orig.text ||
        q.correctAnswer !== orig.correctAnswer ||
        (q.options ?? []).join("|") !== (orig.options ?? []).join("|") ||
        (q.explanation ?? "") !== (orig.explanation ?? "") ||
        q.order !== orig.order
      );
    });
  }, [quiz, title, questions]);

  if (quiz === "loading") {
    return <CenteredMessage text="loading…" />;
  }
  if (quiz === "notfound" || !quiz) {
    return (
      <CenteredMessage>
        <p>Quiz not found.</p>
        <Link to="/" className="mt-3 text-accent underline">Back to dashboard</Link>
      </CenteredMessage>
    );
  }
  // quiz is now narrowed to QuizDetailDto, but TS loses that narrowing
  // inside closures — reassign to a const so handlers can use it directly.
  const loaded: QuizDetailDto = quiz;

  function updateQuestion(idx: number, patch: Partial<QuestionDto>) {
    setQuestions((qs) => qs.map((q, i) => (i === idx ? { ...q, ...patch } : q)));
  }

  function deleteQuestion(idx: number) {
    setQuestions((qs) => qs.filter((_, i) => i !== idx).map((q, i) => ({ ...q, order: i })));
  }

  async function onRegenerate(idx: number) {
    const q = questions[idx];
    try {
      const updated = await regenerateQuestion(loaded.id, q.id, provider, model);
      setQuestions((qs) =>
        qs.map((existing) => (existing.id === q.id ? { ...updated, id: q.id, order: existing.order } : existing)),
      );
    } catch (e) {
      push(e instanceof Error ? e.message : "Regenerate failed", "error");
    }
  }

  async function onSave() {
    setSavingState("saving");
    setSaveError(null);
    try {
      const body = {
        title: title.trim(),
        questions: questions.map<UpdateQuestionRequest>((q) => ({
          id: q.id,
          text: q.text,
          correctAnswer: q.correctAnswer,
          options: q.options ?? null,
          explanation: q.explanation ?? null,
          order: q.order,
          factCheckFlagged: q.factCheckFlagged,
          factCheckNote: q.factCheckNote ?? null,
        })),
      };
      await updateQuiz(loaded.id, body);
      // Refetch so dirty-tracking baseline updates
      const fresh = await getQuiz(loaded.id);
      if (fresh) {
        setQuiz(fresh);
        setQuestions(fresh.questions);
        setTitle(fresh.title);
      }
      setSavingState("saved");
      setTimeout(() => setSavingState((s) => (s === "saved" ? "idle" : s)), 2000);
    } catch (e) {
      setSavingState("error");
      setSaveError(e instanceof Error ? e.message : "Save failed");
    }
  }

  async function onDeleteQuiz() {
    if (!confirm(`Delete quiz "${loaded.title}"? This cannot be undone.`)) return;
    try {
      await deleteQuiz(loaded.id);
      navigate("/", { replace: true });
    } catch (e) {
      push(e instanceof Error ? e.message : "Delete failed", "error");
    }
  }

  const currentProviderModels =
    providers.kind === "ok"
      ? providers.data.providers.find((p) => p.provider === provider)?.models ?? []
      : [];

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <h1 className="text-xl font-semibold tracking-tight">
            <Link to="/">Quizmaster</Link>
            <span className="text-fg-muted"> / quiz</span>
          </h1>
          <div className="flex items-center gap-3 text-sm">
            {savingState === "saved" && <span className="text-fg-muted">saved</span>}
            <ThemePicker />
            <button
              type="button"
              onClick={() => setShowAnswers((v) => !v)}
              className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg-muted hover:text-fg"
            >
              {showAnswers ? "Hide answers" : "Show answers"}
            </button>
            {questions.length > 0 && (
              <button
                type="button"
                onClick={() => setShowFactCheck((v) => !v)}
                className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg-muted hover:text-fg"
              >
                {showFactCheck ? "Hide fact-check" : "Fact-check"}
              </button>
            )}
            {questions.length > 0 && (
              <button
                type="button"
                disabled={dirty}
                title={dirty ? "Save changes first" : "Start a play session"}
                onClick={async () => {
                  try {
                    const s = await startSession(loaded.id);
                    navigate(`/play/${s.id}`);
                  } catch (e) {
                    push(e instanceof Error ? e.message : "Couldn't start session", "error");
                  }
                }}
                className="rounded-md border border-accent/60 bg-accent/10 px-3 py-1 font-medium text-accent hover:bg-accent/20 disabled:opacity-40"
              >
                ▶ Play
              </button>
            )}
            <button
              type="button"
              onClick={onDeleteQuiz}
              className="rounded-md border border-red-500/50 bg-red-500/10 px-3 py-1 text-red-700 dark:text-red-300 hover:bg-red-500/20"
            >
              Delete quiz
            </button>
            <button
              type="button"
              onClick={onSave}
              disabled={!dirty || savingState === "saving"}
              className="rounded-md bg-accent px-4 py-1 font-medium text-accent-fg disabled:opacity-40"
            >
              {savingState === "saving" ? "Saving…" : "Save changes"}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-6 px-6 py-10">
        <div className="rounded-xl border border-border bg-surface p-6 shadow-sm">
          <label className="block">
            <span className="mb-1 block text-sm text-fg-muted">Title</span>
            <input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 text-fg outline-none focus:border-accent"
            />
          </label>

          <div className="mt-4 grid grid-cols-2 gap-4 text-sm text-fg-muted">
            <div>
              Source: <span className="text-fg">{loaded.source}</span> · Created{" "}
              <span className="text-fg">{new Date(loaded.createdAt).toLocaleString()}</span>
            </div>
            <div className="text-right">
              Original: <span className="text-fg">{loaded.providerUsed}</span> /{" "}
              <span className="text-fg">{loaded.modelUsed}</span>
            </div>
          </div>

          {loaded.topics.length > 0 && (
            <div className="mt-4 flex flex-wrap gap-2">
              {loaded.topics.map((t) => (
                <span
                  key={t.name}
                  className="rounded-md border border-border bg-surface-muted px-2 py-1 text-xs"
                >
                  {t.name} ({t.count})
                </span>
              ))}
            </div>
          )}
        </div>

        {showFactCheck && (
          <FactCheckPanel
            questions={questions.map((q) => ({
              topic:            q.topic,
              text:             q.text,
              type:             q.type,
              correctAnswer:    q.correctAnswer,
              options:          q.options,
              explanation:      q.explanation,
              order:            q.order,
              factCheckFlagged: q.factCheckFlagged,
              factCheckNote:    q.factCheckNote,
            }))}
            busy={factCheckBusy}
            error={factCheckError}
            onApplyAi={async (provider, model) => {
              if (!id) return;
              setFactCheckBusy(true);
              setFactCheckError(null);
              try {
                const updated = await factCheckSavedAi(id, provider, model);
                setQuiz(updated);
                setQuestions(updated.questions);
                push("Fact-check applied.", "success");
              } catch (e) {
                setFactCheckError(e instanceof Error ? e.message : "Fact-check failed");
              } finally {
                setFactCheckBusy(false);
              }
            }}
            onApplyJson={async (sourceJson) => {
              if (!id) return;
              setFactCheckBusy(true);
              setFactCheckError(null);
              try {
                const updated = await factCheckSavedJson(id, sourceJson);
                setQuiz(updated);
                setQuestions(updated.questions);
                push("Fact-check applied.", "success");
              } catch (e) {
                setFactCheckError(e instanceof Error ? e.message : "Fact-check failed");
              } finally {
                setFactCheckBusy(false);
              }
            }}
          />
        )}

        {/* Regenerate provider/model picker */}
        {providers.kind === "ok" && (
          <div className="rounded-xl border border-border bg-surface p-4 text-sm">
            <p className="mb-2 text-fg-muted">Regenerate single questions using:</p>
            <div className="grid grid-cols-2 gap-3">
              <select
                value={provider}
                onChange={(e) => setProvider(e.target.value)}
                className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg"
              >
                {providers.data.providers.map((p) => (
                  <option key={p.provider} value={p.provider}>
                    {p.provider}
                  </option>
                ))}
              </select>
              <select
                value={model}
                onChange={(e) => setModel(e.target.value)}
                className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg"
              >
                {currentProviderModels.map((m) => (
                  <option key={m} value={m}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </div>
        )}

        {questions.map((q, i) => (
          <QuestionEditor
            key={q.id}
            index={i}
            q={q}
            showAnswers={showAnswers}
            onChange={(patch) => updateQuestion(i, patch)}
            onDelete={() => deleteQuestion(i)}
            onRegenerate={() => onRegenerate(i)}
          />
        ))}

        {saveError && (
          <div className="rounded-md border border-red-500/40 bg-red-500/10 p-3 text-sm text-red-700 dark:text-red-300">
            {saveError}
          </div>
        )}
      </main>
    </div>
  );
}

function CenteredMessage({ text, children }: { text?: string; children?: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-2 text-fg-muted">
      {text && <p>{text}</p>}
      {children}
    </div>
  );
}

function QuestionEditor({
  index,
  q,
  showAnswers,
  onChange,
  onDelete,
  onRegenerate,
}: {
  index: number;
  q: QuestionDto;
  showAnswers: boolean;
  onChange: (patch: Partial<QuestionDto>) => void;
  onDelete: () => void;
  onRegenerate: () => void;
}) {
  const [busy, setBusy] = useState(false);

  return (
    <div
      className={
        "rounded-md border bg-surface p-4 " +
        (q.factCheckFlagged ? "border-yellow-500/60" : "border-border")
      }
    >
      <div className="flex items-baseline justify-between text-xs">
        <div className="text-fg-muted">
          {index + 1}. {q.topic && <span className="mr-1 font-medium">[{q.topic}]</span>}
          <span>{q.type === "MultipleChoice" ? "Multiple choice" : "Free text"}</span>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              try { await onRegenerate(); } finally { setBusy(false); }
            }}
            className="rounded-md border border-border bg-surface-muted px-2 py-0.5 text-fg-muted hover:text-fg disabled:opacity-50"
          >
            {busy ? "regenerating…" : "regenerate"}
          </button>
          <button
            type="button"
            onClick={onDelete}
            className="rounded-md border border-red-500/40 bg-red-500/10 px-2 py-0.5 text-red-700 dark:text-red-300 hover:bg-red-500/20"
          >
            delete
          </button>
        </div>
      </div>

      <textarea
        value={q.text}
        onChange={(e) => onChange({ text: e.target.value })}
        rows={2}
        className="mt-3 w-full rounded-md border border-border bg-surface-muted px-3 py-2 text-fg outline-none focus:border-accent"
      />

      <label className="mt-3 block text-xs text-fg-muted">
        Correct answer
        {showAnswers ? (
          <input
            value={q.correctAnswer}
            onChange={(e) => onChange({ correctAnswer: e.target.value })}
            className="mt-1 w-full rounded-md border border-border bg-surface-muted px-3 py-1.5 font-mono text-sm text-fg outline-none focus:border-accent"
          />
        ) : (
          <div className="mt-1 w-full rounded-md border border-dashed border-border bg-surface-muted px-3 py-1.5 font-mono text-sm text-fg-muted/60 select-none">
            ••••••••
          </div>
        )}
      </label>

      {q.type === "MultipleChoice" && q.options && (
        <div className="mt-3">
          <div className="text-xs text-fg-muted">Options</div>
          {q.options.map((opt, oi) => (
            <input
              key={oi}
              value={opt}
              onChange={(e) => {
                const options = [...(q.options ?? [])];
                options[oi] = e.target.value;
                onChange({ options });
              }}
              className={
                "mt-1 w-full rounded-md border bg-surface-muted px-3 py-1.5 text-sm outline-none focus:border-accent " +
                (showAnswers && opt === q.correctAnswer
                  ? "border-accent text-fg"
                  : "border-border text-fg-muted")
              }
            />
          ))}
        </div>
      )}

      <label className="mt-3 block text-xs text-fg-muted">
        Explanation
        <input
          value={showAnswers ? (q.explanation ?? "") : (q.explanation ? "••••••••" : "")}
          disabled={!showAnswers}
          onChange={(e) => onChange({ explanation: e.target.value || null })}
          placeholder="(optional)"
          className="mt-1 w-full rounded-md border border-border bg-surface-muted px-3 py-1.5 text-sm text-fg outline-none focus:border-accent"
        />
      </label>

      {q.factCheckNote && (
        <p className="mt-2 text-xs text-yellow-700 dark:text-yellow-300">
          fact-check: {q.factCheckNote}
        </p>
      )}
    </div>
  );
}
