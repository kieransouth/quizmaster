import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch } from "../api/client";
import type { UserApiKeyStatus } from "../ai/types";
import { ThemePicker } from "../ui/ThemePicker";
import { useToast } from "../ui/toast";

type LoadState =
  | { kind: "loading" }
  | { kind: "ok"; statuses: UserApiKeyStatus[] }
  | { kind: "error"; message: string };

export default function Settings() {
  const { push } = useToast();
  const [state, setState] = useState<LoadState>({ kind: "loading" });

  // Bumped after each successful set/clear to force a refetch.
  const [reloadTick, setReloadTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await apiFetch("/api/me/ai-providers");
        if (cancelled) return;
        if (!res.ok) {
          setState({ kind: "error", message: `Failed (HTTP ${res.status})` });
          return;
        }
        const statuses = (await res.json()) as UserApiKeyStatus[];
        if (!cancelled) setState({ kind: "ok", statuses });
      } catch (e) {
        if (!cancelled) {
          setState({ kind: "error", message: e instanceof Error ? e.message : "Failed to load" });
        }
      }
    })();
    return () => { cancelled = true; };
  }, [reloadTick]);

  const reload = () => setReloadTick((n) => n + 1);

  async function setKey(provider: string, apiKey: string) {
    const res = await apiFetch(`/api/me/ai-providers/${encodeURIComponent(provider)}`, {
      method: "PUT",
      body: JSON.stringify({ apiKey }),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      push((body?.error as string | undefined) ?? "Failed to save key.", "error");
      return;
    }
    push(`${provider} key saved.`, "success");
    reload();
  }

  async function clearKey(provider: string) {
    const res = await apiFetch(`/api/me/ai-providers/${encodeURIComponent(provider)}`, {
      method: "DELETE",
    });
    if (!res.ok) {
      push("Failed to clear key.", "error");
      return;
    }
    push(`${provider} key cleared.`, "success");
    reload();
  }

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-6 py-4">
          <div className="flex items-center gap-3 text-sm text-fg-muted">
            <Link to="/">← Quizzes</Link>
            <span>·</span>
            <span className="text-fg">Settings</span>
          </div>
          <ThemePicker />
        </div>
      </header>

      <main className="mx-auto max-w-3xl space-y-8 px-6 py-10">
        <div>
          <h1 className="text-2xl font-semibold">AI provider keys</h1>
          <p className="mt-2 text-sm text-fg-muted">
            Cloud providers (OpenAI, Anthropic) call against your own API keys.
            Keys are encrypted at rest and never sent back to your browser
            after saving — only a masked preview is shown. Ollama, when
            enabled, runs on the server's own daemon and doesn't need a key.
          </p>
        </div>

        {state.kind === "loading" && <p className="text-sm text-fg-muted">loading…</p>}
        {state.kind === "error" && (
          <div className="rounded-md border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-300">
            {state.message}
          </div>
        )}
        {state.kind === "ok" && state.statuses.length === 0 && (
          <p className="text-sm text-fg-muted">No providers are enabled on this server.</p>
        )}
        {state.kind === "ok" && state.statuses.length > 0 && (
          <ul className="space-y-3">
            {state.statuses.map((s) => (
              <ProviderRow
                key={s.provider}
                status={s}
                onSet={(key) => setKey(s.provider, key)}
                onClear={() => clearKey(s.provider)}
              />
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}

function ProviderRow({
  status,
  onSet,
  onClear,
}: {
  status: UserApiKeyStatus;
  onSet: (apiKey: string) => Promise<void>;
  onClear: () => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);

  // Ollama is the no-key provider — show a flat enabled status.
  const isOllama = status.provider === "Ollama";

  async function save() {
    if (!draft.trim()) return;
    setBusy(true);
    try {
      await onSet(draft.trim());
      setDraft("");
      setEditing(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <li className="rounded-md border border-border bg-surface p-4">
      <div className="flex items-baseline justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="font-medium">{status.provider}</div>
          <div className="mt-1 truncate text-xs text-fg-muted">
            {isOllama
              ? "Server-shared. No key required."
              : status.hasKey
                ? <>Key on file: <span className="font-mono">{status.masked}</span></>
                : "No key on file."}
          </div>
        </div>
        {!isOllama && (
          <div className="flex gap-2">
            {status.hasKey && !editing && (
              <button
                type="button"
                onClick={onClear}
                disabled={busy}
                className="rounded-md border border-border bg-surface-muted px-3 py-1 text-xs text-fg-muted hover:text-fg disabled:opacity-50"
              >
                Clear
              </button>
            )}
            {!editing && (
              <button
                type="button"
                onClick={() => setEditing(true)}
                disabled={busy}
                className="rounded-md border border-accent/50 bg-accent/10 px-3 py-1 text-xs text-accent disabled:opacity-50"
              >
                {status.hasKey ? "Replace" : "Set key"}
              </button>
            )}
          </div>
        )}
      </div>

      {editing && (
        <div className="mt-3 flex flex-col gap-2 sm:flex-row">
          <input
            type="password"
            autoFocus
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder={`${status.provider} API key`}
            className="flex-1 rounded-md border border-border bg-surface-muted px-3 py-2 font-mono text-sm text-fg outline-none focus:border-accent"
          />
          <div className="flex gap-2">
            <button
              type="button"
              onClick={save}
              disabled={busy || !draft.trim()}
              className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-fg disabled:opacity-50"
            >
              {busy ? "Saving…" : "Save"}
            </button>
            <button
              type="button"
              onClick={() => { setEditing(false); setDraft(""); }}
              disabled={busy}
              className="rounded-md border border-border bg-surface-muted px-3 py-2 text-sm text-fg-muted hover:text-fg disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </li>
  );
}
