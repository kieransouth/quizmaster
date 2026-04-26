import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useProviders } from "../ai/useProviders";
import { buildFactCheckPrompt } from "./factCheckPromptTemplate";
import type { DraftQuestion } from "./types";

interface Props {
  questions:   DraftQuestion[];
  busy:        boolean;
  onApplyAi:   (provider: string, model: string) => Promise<void>;
  onApplyJson: (sourceJson: string) => Promise<void>;
  /** Optional inline error from the parent (e.g. API rejection). */
  error?:      string | null;
}

type Tab = "ai" | "byo";

/**
 * Standalone fact-check panel reused by NewQuiz (post-generation, pre-save)
 * and QuizDetail (saved quiz). Two tabs: AI (provider/model dropdown,
 * server runs the model) and BYO (prompt + paste box, no server-side AI).
 */
export function FactCheckPanel({ questions, busy, onApplyAi, onApplyJson, error }: Props) {
  const providers = useProviders();
  const [tab, setTab] = useState<Tab>("ai");
  const [provider, setProvider] = useState("");
  const [model, setModel] = useState("");
  const [pasted, setPasted] = useState("");
  const [copied, setCopied] = useState(false);

  // Seed provider/model defaults once providers load + snap model to a
  // valid choice when provider changes. The set-state-in-effect rule is
  // overly conservative here — both writes are guarded by
  // data-not-yet-matched conditions, and NewQuiz.tsx uses the same
  // pattern. Disabled per-line below.
  useEffect(() => {
    if (providers.kind !== "ok") return;
    if (provider) return;
    const first = providers.data.providers[0];
    if (!first) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setProvider(first.provider);
    setModel(first.models[0] ?? "");
  }, [providers, provider]);

  useEffect(() => {
    if (providers.kind !== "ok") return;
    const p = providers.data.providers.find((x) => x.provider === provider);
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (p && !p.models.includes(model)) setModel(p.models[0] ?? "");
  }, [provider, model, providers]);

  const prompt = useMemo(() => buildFactCheckPrompt(questions), [questions]);
  const currentModels =
    providers.kind === "ok"
      ? providers.data.providers.find((p) => p.provider === provider)?.models ?? []
      : [];

  async function copyPrompt() {
    try {
      await navigator.clipboard.writeText(prompt);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard blocked — user can select the textarea contents manually.
    }
  }

  return (
    <section className="rounded-md border border-border bg-surface p-4">
      <header className="mb-3 flex items-center justify-between">
        <div>
          <h3 className="text-sm font-medium">Fact-check</h3>
          <p className="mt-0.5 text-xs text-fg-muted">
            Optional. Audit each answer with an independent model — flagged
            questions show inline so you can fix them before save.
          </p>
        </div>
        <div role="tablist" className="flex gap-1 rounded-md border border-border bg-surface-muted p-1 text-xs">
          {(["ai", "byo"] as Tab[]).map((t) => (
            <button
              key={t}
              type="button"
              role="tab"
              aria-selected={tab === t}
              onClick={() => setTab(t)}
              disabled={busy}
              className={
                "rounded-md px-3 py-1 " +
                (tab === t ? "bg-accent text-accent-fg" : "text-fg-muted hover:text-fg")
              }
            >
              {t === "ai" ? "AI" : "BYO AI"}
            </button>
          ))}
        </div>
      </header>

      {tab === "ai" && (
        <div className="space-y-3">
          {providers.kind === "loading" && (
            <p className="text-sm text-fg-muted">loading providers…</p>
          )}
          {providers.kind === "error" && (
            <p className="text-sm text-red-500">failed to load providers</p>
          )}
          {providers.kind === "ok" && providers.data.providers.length === 0 && (
            <div className="rounded-md border border-amber-500/40 bg-amber-500/10 p-3 text-xs text-amber-700 dark:text-amber-300">
              No AI providers available for fact-check. Add an OpenAI or
              Anthropic key in <Link to="/settings" className="underline">Settings</Link>,
              or use the BYO AI tab to fact-check externally.
            </div>
          )}
          {providers.kind === "ok" && providers.data.providers.length > 0 && (
            <>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="mb-1 block text-xs text-fg-muted">Provider</label>
                  <select
                    value={provider}
                    onChange={(e) => setProvider(e.target.value)}
                    disabled={busy}
                    className="w-full rounded-md border border-border bg-surface-muted px-2 py-2 text-sm text-fg"
                  >
                    {providers.data.providers.map((p) => (
                      <option key={p.provider} value={p.provider}>{p.provider}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs text-fg-muted">Model</label>
                  <select
                    value={model}
                    onChange={(e) => setModel(e.target.value)}
                    disabled={busy}
                    className="w-full rounded-md border border-border bg-surface-muted px-2 py-2 text-sm text-fg"
                  >
                    {currentModels.map((m) => (
                      <option key={m} value={m}>{m}</option>
                    ))}
                  </select>
                </div>
              </div>
              <button
                type="button"
                onClick={() => void onApplyAi(provider, model)}
                disabled={busy || !provider || !model || questions.length === 0}
                className="w-full rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-fg disabled:opacity-50"
              >
                {busy ? "Running fact-check…" : "Run fact-check"}
              </button>
            </>
          )}
        </div>
      )}

      {tab === "byo" && (
        <div className="space-y-3">
          <div>
            <div className="mb-1 flex items-center justify-between">
              <label className="text-xs text-fg-muted">
                Step 1: copy this prompt into any AI tool
              </label>
              <button
                type="button"
                onClick={copyPrompt}
                disabled={busy || questions.length === 0}
                className="rounded-md border border-border bg-surface-muted px-2 py-1 text-xs text-fg-muted hover:text-fg disabled:opacity-50"
              >
                {copied ? "Copied!" : "Copy"}
              </button>
            </div>
            <textarea
              readOnly
              value={prompt}
              rows={6}
              className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-xs text-fg outline-none"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs text-fg-muted">
              Step 2: paste the AI's JSON response here
            </label>
            <textarea
              value={pasted}
              onChange={(e) => setPasted(e.target.value)}
              disabled={busy}
              rows={6}
              placeholder={`{\n  "checks": [\n    { "questionIndex": 0, "factuallyCorrect": true, "note": null }\n  ]\n}`}
              className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-xs text-fg outline-none focus:border-accent"
            />
          </div>

          <button
            type="button"
            onClick={() => void onApplyJson(pasted)}
            disabled={busy || !pasted.trim() || questions.length === 0}
            className="w-full rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-fg disabled:opacity-50"
          >
            {busy ? "Applying…" : "Apply fact-check"}
          </button>
        </div>
      )}

      {error && (
        <div className="mt-3 rounded-md border border-red-500/40 bg-red-500/10 p-3 text-xs text-red-700 dark:text-red-300">
          {error}
        </div>
      )}
    </section>
  );
}
