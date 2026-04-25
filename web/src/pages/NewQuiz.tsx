import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useProviders } from "../ai/useProviders";
import { saveQuiz } from "../quizzes/api";
import { useGenerationStream } from "../quizzes/useGenerationStream";
import { buildGenerationPrompt } from "../quizzes/promptTemplate";
import type {
  DraftQuestion,
  GenerateQuizRequest,
  ImportFromJsonRequest,
  ImportQuizRequest,
  TopicRequest,
} from "../quizzes/types";

type Mode = "generate" | "import" | "byo";

export default function NewQuiz() {
  const navigate = useNavigate();
  const providers = useProviders();
  const stream = useGenerationStream();
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  // Hide answers by default so the host can play along with the team
  // without spoiling themselves while reviewing the generated quiz.
  const [showAnswers, setShowAnswers] = useState(false);

  const [mode, setMode] = useState<Mode>("generate");
  const [title, setTitle] = useState("");
  const [topics, setTopics] = useState<TopicRequest[]>([{ name: "", count: 5 }]);
  const [mcFraction, setMcFraction] = useState(0.5);
  const [sourceText, setSourceText] = useState("");
  const [factCheck, setFactCheck] = useState(false);
  const [provider, setProvider] = useState<string>("");
  const [model, setModel] = useState<string>("");
  // Independent fact-check provider/model. Asking a different model to
  // verify is the whole point of fact-check; same model grading itself
  // is mostly a vibe check.
  const [factCheckProvider, setFactCheckProvider] = useState<string>("");
  const [factCheckModel, setFactCheckModel] = useState<string>("");
  // BYO-AI mode: user pastes JSON from an external AI tool.
  const [pastedJson, setPastedJson] = useState("");
  const [promptCopied, setPromptCopied] = useState(false);

  // Seed provider/model defaults once the providers list loads.
  useEffect(() => {
    if (providers.kind !== "ok") return;
    if (!provider) {
      setProvider(providers.data.defaultProvider);
      setModel(providers.data.defaultModel);
    }
  }, [providers, provider]);

  // Default the fact-check pair to a *different* provider when more than
  // one is configured; otherwise the default provider but a different model.
  useEffect(() => {
    if (providers.kind !== "ok" || !provider) return;
    if (factCheckProvider) return;
    const otherProvider = providers.data.providers.find((p) => p.provider !== provider);
    if (otherProvider) {
      setFactCheckProvider(otherProvider.provider);
      setFactCheckModel(otherProvider.models[0] ?? "");
    } else {
      const same = providers.data.providers.find((p) => p.provider === provider);
      const altModel = same?.models.find((m) => m !== model) ?? same?.models[0] ?? "";
      setFactCheckProvider(provider);
      setFactCheckModel(altModel);
    }
  }, [providers, provider, model, factCheckProvider]);

  // When provider changes, snap model to a valid choice for that provider.
  useEffect(() => {
    if (providers.kind !== "ok") return;
    const p = providers.data.providers.find((x) => x.provider === provider);
    if (p && !p.models.includes(model)) {
      setModel(p.models[0] ?? "");
    }
  }, [provider, model, providers]);

  // Same snap behaviour for the fact-check pair.
  useEffect(() => {
    if (providers.kind !== "ok") return;
    const p = providers.data.providers.find((x) => x.provider === factCheckProvider);
    if (p && !p.models.includes(factCheckModel)) {
      setFactCheckModel(p.models[0] ?? "");
    }
  }, [factCheckProvider, factCheckModel, providers]);

  // Derived state from the event stream.
  const status = useMemo(
    () => [...stream.events].reverse().find((e) => e.type === "status")?.stage,
    [stream.events],
  );
  // Dedupe by order — fact-check re-emits questions with updated flags,
  // and we want the latest version per slot.
  const questions = useMemo(() => {
    const byOrder = new Map<number, DraftQuestion>();
    for (const e of stream.events) {
      if (e.type === "question") byOrder.set(e.item.order, e.item);
    }
    return [...byOrder.values()].sort((a, b) => a.order - b.order);
  }, [stream.events]);
  const warnings = useMemo(
    () => stream.events.filter((e): e is { type: "warning"; message: string } => e.type === "warning"),
    [stream.events],
  );
  const errorEvent = useMemo(
    () => stream.events.find((e) => e.type === "error"),
    [stream.events],
  );
  const doneEvent = useMemo(
    () => stream.events.find((e) => e.type === "done"),
    [stream.events],
  );

  const totalRequested = topics.reduce((sum, t) => sum + (t.count || 0), 0);
  const validToSubmit =
    title.trim().length > 0 &&
    (mode === "byo"
      ? pastedJson.trim().length > 0
      : provider && model &&
        (mode === "generate"
          ? topics.length > 0 && topics.every((t) => t.name.trim()) && totalRequested > 0
          : sourceText.trim().length > 0));

  const generationPrompt =
    mode === "byo" ? buildGenerationPrompt(topics, mcFraction) : "";

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validToSubmit || stream.running) return;

    if (mode === "byo") {
      const req: ImportFromJsonRequest = {
        title: title.trim(),
        topics: topics
          .filter((t) => t.name.trim())
          .map((t) => ({ name: t.name.trim(), count: t.count })),
        sourceJson: pastedJson,
      };
      await stream.start("/api/quizzes/import-json", req);
    } else if (mode === "generate") {
      const req: GenerateQuizRequest = {
        title: title.trim(),
        topics: topics.map((t) => ({ name: t.name.trim(), count: t.count })),
        multipleChoiceFraction: mcFraction,
        runFactCheck: factCheck,
        provider,
        model,
        // Only send fact-check pair when fact-check is enabled.
        ...(factCheck && factCheckProvider
          ? { factCheckProvider, factCheckModel }
          : {}),
      };
      await stream.start("/api/quizzes/generate", req);
    } else {
      const req: ImportQuizRequest = {
        title: title.trim(),
        sourceText: sourceText.trim(),
        runFactCheck: factCheck,
        provider,
        model,
        ...(factCheck && factCheckProvider
          ? { factCheckProvider, factCheckModel }
          : {}),
      };
      await stream.start("/api/quizzes/import", req);
    }
  }

  function onRetry() {
    stream.reset();
    void onSubmit({ preventDefault: () => {} } as React.FormEvent);
  }

  const currentProviderModels =
    providers.kind === "ok"
      ? providers.data.providers.find((p) => p.provider === provider)?.models ?? []
      : [];
  const currentFactCheckModels =
    providers.kind === "ok"
      ? providers.data.providers.find((p) => p.provider === factCheckProvider)?.models ?? []
      : [];

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <h1 className="text-xl font-semibold tracking-tight">
            <Link to="/">Quizmaster</Link>
            <span className="text-fg-muted"> / new quiz</span>
          </h1>
        </div>
      </header>

      <main className="mx-auto grid max-w-6xl grid-cols-1 gap-6 px-6 py-10 lg:grid-cols-[420px,1fr]">
        {/* ---- LEFT: form ---- */}
        <form onSubmit={onSubmit} className="space-y-6">
          <ModeToggle mode={mode} onChange={setMode} disabled={stream.running} />

          <div>
            <label className="mb-1 block text-sm text-fg-muted">Title</label>
            <input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={stream.running}
              className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 text-fg outline-none focus:border-accent"
              placeholder={mode === "generate" ? "Star Wars night" : "Pub quiz March 12"}
            />
          </div>

          {mode === "generate" || mode === "byo" ? (
            <>
              <TopicsEditor topics={topics} onChange={setTopics} disabled={stream.running} />
              <McMixSlider value={mcFraction} onChange={setMcFraction} disabled={stream.running} />
              <p className="text-sm text-fg-muted">
                Total: <span className="text-fg">{totalRequested}</span> questions
              </p>
            </>
          ) : (
            <div>
              <label className="mb-1 block text-sm text-fg-muted">Source text</label>
              <textarea
                value={sourceText}
                onChange={(e) => setSourceText(e.target.value)}
                disabled={stream.running}
                rows={10}
                className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-sm text-fg outline-none focus:border-accent"
                placeholder={`Paste a quiz here. e.g.\n\n1. What year did WWII end? — 1945\n2. Capital of Australia? — Canberra`}
              />
            </div>
          )}

          {mode === "byo" && (
            <ByoAiPanel
              prompt={generationPrompt}
              copied={promptCopied}
              onCopy={async () => {
                try {
                  await navigator.clipboard.writeText(generationPrompt);
                  setPromptCopied(true);
                  setTimeout(() => setPromptCopied(false), 2000);
                } catch {
                  /* clipboard blocked — user can select manually */
                }
              }}
              json={pastedJson}
              onJsonChange={setPastedJson}
              disabled={stream.running}
            />
          )}

          {mode !== "byo" && providers.kind === "loading" && (
            <p className="text-sm text-fg-muted">loading providers…</p>
          )}
          {mode !== "byo" && providers.kind === "error" && (
            <p className="text-sm text-red-500">failed to load providers</p>
          )}
          {mode !== "byo" && providers.kind === "ok" && (
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="mb-1 block text-sm text-fg-muted">Provider</label>
                <select
                  value={provider}
                  onChange={(e) => setProvider(e.target.value)}
                  disabled={stream.running}
                  className="w-full rounded-md border border-border bg-surface-muted px-2 py-2 text-fg"
                >
                  {providers.data.providers.map((p) => (
                    <option key={p.provider} value={p.provider}>
                      {p.provider}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm text-fg-muted">Model</label>
                <select
                  value={model}
                  onChange={(e) => setModel(e.target.value)}
                  disabled={stream.running}
                  className="w-full rounded-md border border-border bg-surface-muted px-2 py-2 text-fg"
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

          {mode !== "byo" && (
            <label className="flex items-center gap-2 text-sm text-fg-muted">
              <input
                type="checkbox"
                checked={factCheck}
                onChange={(e) => setFactCheck(e.target.checked)}
                disabled={stream.running}
              />
              Run AI fact-check (slower, second model call)
            </label>
          )}

          {factCheck && providers.kind === "ok" && (
            <div className="space-y-2 rounded-md border border-border bg-surface-muted p-3">
              <p className="text-xs text-fg-muted">
                Fact-check using a different model gives independent verification
                rather than asking the same model to grade itself.
              </p>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="mb-1 block text-xs text-fg-muted">
                    Fact-check provider
                  </label>
                  <select
                    value={factCheckProvider}
                    onChange={(e) => setFactCheckProvider(e.target.value)}
                    disabled={stream.running}
                    className="w-full rounded-md border border-border bg-surface px-2 py-1.5 text-fg"
                  >
                    {providers.data.providers.map((p) => (
                      <option key={p.provider} value={p.provider}>
                        {p.provider}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs text-fg-muted">
                    Fact-check model
                  </label>
                  <select
                    value={factCheckModel}
                    onChange={(e) => setFactCheckModel(e.target.value)}
                    disabled={stream.running}
                    className="w-full rounded-md border border-border bg-surface px-2 py-1.5 text-fg"
                  >
                    {currentFactCheckModels.map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          )}

          <button
            type="submit"
            disabled={!validToSubmit || stream.running}
            className="w-full rounded-md bg-accent px-4 py-2 font-medium text-accent-fg disabled:opacity-50"
          >
            {stream.running
              ? status
                ? `${status}…`
                : "Working…"
              : mode === "generate"
                ? "Generate"
                : mode === "byo"
                  ? "Import JSON"
                  : "Extract"}
          </button>

          {stream.running && (
            <button
              type="button"
              onClick={() => stream.abort()}
              className="w-full rounded-md border border-border bg-surface-muted px-4 py-2 text-sm text-fg-muted hover:bg-surface"
            >
              Cancel
            </button>
          )}
        </form>

        {/* ---- RIGHT: results ---- */}
        <section className="space-y-4">
          {status && stream.running && (
            <div className="rounded-md border border-border bg-surface p-4 text-sm text-fg-muted">
              <span className="inline-block animate-pulse">●</span> {status}…
            </div>
          )}

          {warnings.map((w, i) => (
            <div
              key={i}
              className="rounded-md border border-yellow-500/40 bg-yellow-500/10 p-3 text-sm text-yellow-700 dark:text-yellow-300"
            >
              ⚠ {w.message}
            </div>
          ))}

          {errorEvent && errorEvent.type === "error" && (
            <div className="rounded-md border border-red-500/40 bg-red-500/10 p-4">
              <p className="font-medium text-red-700 dark:text-red-300">{errorEvent.message}</p>
              {errorEvent.retryable && (
                <button
                  onClick={onRetry}
                  className="mt-3 rounded-md bg-red-500 px-3 py-1 text-sm font-medium text-white hover:bg-red-600"
                >
                  Retry
                </button>
              )}
            </div>
          )}

          {questions.length === 0 && !stream.running && !errorEvent && (
            <div className="rounded-md border border-dashed border-border p-10 text-center text-sm text-fg-muted">
              Questions will appear here as they're generated.
            </div>
          )}

          {questions.length > 0 && (
            <div className="flex items-center justify-between rounded-md border border-border bg-surface px-4 py-2 text-sm">
              <span className="text-fg-muted">
                {showAnswers
                  ? "Answers visible — for review."
                  : "Answers hidden so you can play along."}
              </span>
              <button
                type="button"
                onClick={() => setShowAnswers((v) => !v)}
                className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg-muted hover:text-fg"
              >
                {showAnswers ? "Hide answers" : "Show answers"}
              </button>
            </div>
          )}

          {questions.map((q, i) => (
            <QuestionCard key={i} index={i} q={q} showAnswers={showAnswers} />
          ))}

          {doneEvent && doneEvent.type === "done" && (
            <div className="rounded-md border border-accent/40 bg-accent/10 p-4">
              <p className="font-medium">
                Done — {doneEvent.quiz.questions.length} question
                {doneEvent.quiz.questions.length === 1 ? "" : "s"}.
              </p>
              <p className="mt-1 text-sm text-fg-muted">
                Save it to the question bank — you can edit and play it
                from there.
              </p>
              <div className="mt-3 flex items-center gap-3">
                <button
                  type="button"
                  disabled={saving}
                  onClick={async () => {
                    setSaving(true);
                    setSaveError(null);
                    try {
                      const { id } = await saveQuiz(doneEvent.quiz);
                      navigate(`/quizzes/${id}`);
                    } catch (e) {
                      setSaveError(e instanceof Error ? e.message : "Save failed");
                    } finally {
                      setSaving(false);
                    }
                  }}
                  className="rounded-md bg-accent px-4 py-2 font-medium text-accent-fg disabled:opacity-50"
                >
                  {saving ? "Saving…" : "Save quiz"}
                </button>
                {saveError && <span className="text-sm text-red-500">{saveError}</span>}
              </div>
            </div>
          )}
        </section>
      </main>
    </div>
  );
}

function ByoAiPanel({
  prompt,
  copied,
  onCopy,
  json,
  onJsonChange,
  disabled,
}: {
  prompt: string;
  copied: boolean;
  onCopy: () => void;
  json: string;
  onJsonChange: (v: string) => void;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-4 rounded-md border border-accent/40 bg-accent/5 p-3">
      <div>
        <div className="mb-1 flex items-center justify-between text-xs text-fg-muted">
          <span>Prompt to paste into your AI</span>
          <button
            type="button"
            onClick={onCopy}
            disabled={disabled}
            className="rounded-md border border-border bg-surface-muted px-2 py-0.5 text-fg-muted hover:text-fg disabled:opacity-50"
          >
            {copied ? "copied" : "copy"}
          </button>
        </div>
        <textarea
          value={prompt}
          readOnly
          rows={10}
          className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-xs text-fg-muted"
        />
        <p className="mt-1 text-xs text-fg-muted">
          Edit topics + mix on the left to update the prompt. Run it in
          ChatGPT, Claude.ai, anywhere — then paste the JSON below.
        </p>
      </div>

      <div>
        <label className="mb-1 block text-xs text-fg-muted">
          AI's JSON response
        </label>
        <textarea
          value={json}
          onChange={(e) => onJsonChange(e.target.value)}
          disabled={disabled}
          rows={10}
          placeholder={'{ "questions": [ ... ] }'}
          className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-xs text-fg outline-none focus:border-accent"
        />
      </div>
    </div>
  );
}

function ModeToggle({
  mode,
  onChange,
  disabled,
}: {
  mode: Mode;
  onChange: (m: Mode) => void;
  disabled?: boolean;
}) {
  return (
    <div className="inline-flex rounded-md border border-border bg-surface-muted p-1 text-sm">
      {(["generate", "import", "byo"] as Mode[]).map((m) => (
        <button
          key={m}
          type="button"
          onClick={() => onChange(m)}
          disabled={disabled}
          className={
            "rounded px-3 py-1 transition " +
            (mode === m
              ? "bg-accent text-accent-fg"
              : "text-fg-muted hover:text-fg")
          }
        >
          {m === "generate" ? "Generate" : m === "import" ? "Import" : "BYO AI"}
        </button>
      ))}
    </div>
  );
}

function TopicsEditor({
  topics,
  onChange,
  disabled,
}: {
  topics: TopicRequest[];
  onChange: (t: TopicRequest[]) => void;
  disabled?: boolean;
}) {
  return (
    <div>
      <label className="mb-2 flex items-center justify-between text-sm text-fg-muted">
        Topics
        <button
          type="button"
          onClick={() => onChange([...topics, { name: "", count: 5 }])}
          disabled={disabled}
          className="rounded-md border border-border bg-surface-muted px-2 py-0.5 text-xs hover:bg-surface"
        >
          + add
        </button>
      </label>
      <div className="space-y-2">
        {topics.map((t, i) => (
          <div key={i} className="flex items-center gap-2">
            <input
              value={t.name}
              onChange={(e) =>
                onChange(topics.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))
              }
              disabled={disabled}
              placeholder="Star Wars"
              className="flex-1 rounded-md border border-border bg-surface-muted px-3 py-1.5 text-fg outline-none focus:border-accent"
            />
            <input
              type="number"
              min={1}
              max={50}
              value={t.count}
              onChange={(e) =>
                onChange(
                  topics.map((x, j) =>
                    j === i ? { ...x, count: Math.max(1, parseInt(e.target.value) || 1) } : x,
                  ),
                )
              }
              disabled={disabled}
              className="w-16 rounded-md border border-border bg-surface-muted px-2 py-1.5 text-fg outline-none focus:border-accent"
            />
            <button
              type="button"
              onClick={() => onChange(topics.filter((_, j) => j !== i))}
              disabled={disabled || topics.length === 1}
              className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg-muted hover:text-fg disabled:opacity-30"
              aria-label="remove topic"
            >
              ×
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}

function McMixSlider({
  value,
  onChange,
  disabled,
}: {
  value: number;
  onChange: (v: number) => void;
  disabled?: boolean;
}) {
  const mc = Math.round(value * 100);
  return (
    <div>
      <label className="mb-1 block text-sm text-fg-muted">
        Question mix:{" "}
        <span className="text-fg">{mc}% multiple choice</span>,{" "}
        <span className="text-fg">{100 - mc}% free text</span>
      </label>
      <input
        type="range"
        min={0}
        max={100}
        value={mc}
        onChange={(e) => onChange(parseInt(e.target.value) / 100)}
        disabled={disabled}
        className="w-full accent-[var(--color-accent)]"
      />
    </div>
  );
}

function QuestionCard({
  index,
  q,
  showAnswers,
}: {
  index: number;
  q: DraftQuestion;
  showAnswers: boolean;
}) {
  return (
    <div
      className={
        "rounded-md border bg-surface p-4 transition " +
        (q.factCheckFlagged
          ? "border-yellow-500/60"
          : "border-border")
      }
    >
      <div className="flex items-baseline justify-between">
        <div className="text-xs text-fg-muted">
          {index + 1}. {q.topic && <span className="mr-1 font-medium">[{q.topic}]</span>}
          <span>{q.type === "MultipleChoice" ? "Multiple choice" : "Free text"}</span>
        </div>
        {q.factCheckFlagged && (
          <span className="text-xs text-yellow-700 dark:text-yellow-300">⚠ flagged</span>
        )}
      </div>
      <p className="mt-2 font-medium">{q.text}</p>

      {q.type === "MultipleChoice" && q.options && (
        <ul className="mt-2 space-y-1 text-sm">
          {q.options.map((o, i) => (
            <li
              key={i}
              className={
                "rounded px-2 py-1 " +
                (showAnswers && o === q.correctAnswer
                  ? "bg-accent/20 text-fg"
                  : "text-fg-muted")
              }
            >
              {o}
              {showAnswers && o === q.correctAnswer && " ✓"}
            </li>
          ))}
        </ul>
      )}

      {q.type === "FreeText" && (
        <p className="mt-2 text-sm">
          <span className="text-fg-muted">Answer: </span>
          {showAnswers ? (
            <span className="font-mono text-accent">{q.correctAnswer}</span>
          ) : (
            <span className="font-mono text-fg-muted/60 select-none">••••••••</span>
          )}
        </p>
      )}

      {showAnswers && q.explanation && (
        <p className="mt-2 text-xs text-fg-muted italic">{q.explanation}</p>
      )}
      {q.factCheckNote && (
        <p className="mt-2 text-xs text-yellow-700 dark:text-yellow-300">
          fact-check: {q.factCheckNote}
        </p>
      )}
    </div>
  );
}
