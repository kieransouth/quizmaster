import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useProviders } from "../ai/useProviders";
import { apiFetch } from "../api/client";
import { useAuthStore } from "../auth/store";
import { applyTheme, getStoredTheme, type Theme } from "../theme";

type Health = { status: string; version: string };

export default function Home() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const clearAuth = useAuthStore((s) => s.clearAuth);
  const [theme, setTheme] = useState<Theme>(getStoredTheme());
  const [health, setHealth] = useState<Health | "loading" | "error">("loading");

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    apiFetch("/api/health")
      .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
      .then((data: Health) => setHealth(data))
      .catch(() => setHealth("error"));
  }, []);

  async function onLogout() {
    await fetch("/api/auth/logout", { method: "POST", credentials: "include" });
    clearAuth();
    navigate("/login", { replace: true });
  }

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <h1 className="text-xl font-semibold tracking-tight">Quizmaster</h1>
          <div className="flex items-center gap-4 text-sm">
            <span className="text-fg-muted">Hi, {user?.displayName}</span>
            <ThemePicker value={theme} onChange={setTheme} />
            <button
              onClick={onLogout}
              className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg hover:bg-surface"
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-6 px-6 py-16">
        <div className="rounded-xl border border-border bg-surface p-8 shadow-sm">
          <h2 className="text-2xl font-semibold">You're signed in.</h2>
          <p className="mt-2 text-fg-muted">
            Phase 4 complete — AI providers are wired. Phase 5 will start
            generating quizzes.
          </p>

          <div className="mt-6 rounded-lg bg-surface-muted p-4 font-mono text-sm">
            <div className="text-fg-muted">GET /api/health</div>
            <div className="mt-2">
              {health === "loading" && "loading…"}
              {health === "error" && <span className="text-red-500">unreachable</span>}
              {typeof health === "object" && (
                <>
                  status: <span className="text-accent">{health.status}</span>
                  <br />
                  version: <span className="text-accent">{health.version}</span>
                </>
              )}
            </div>
          </div>
        </div>

        <ProvidersPanel />
      </main>
    </div>
  );
}

function ProvidersPanel() {
  const state = useProviders();
  return (
    <div className="rounded-xl border border-border bg-surface p-8 shadow-sm">
      <h3 className="text-lg font-semibold">AI providers</h3>
      <p className="mt-1 text-sm text-fg-muted">
        Configured providers and the models the API will accept for each.
      </p>

      {state.kind === "loading" && <p className="mt-4 text-sm text-fg-muted">loading…</p>}
      {state.kind === "error" && (
        <p className="mt-4 text-sm text-red-500">
          failed to load providers (status {state.status})
        </p>
      )}
      {state.kind === "ok" && (
        <div className="mt-4 space-y-3">
          <p className="text-sm text-fg-muted">
            Default:{" "}
            <span className="text-fg">{state.data.defaultProvider}</span> /{" "}
            <span className="text-fg">{state.data.defaultModel}</span>
          </p>
          {state.data.providers.map((p) => (
            <div key={p.provider} className="rounded-lg bg-surface-muted p-4">
              <div className="font-medium">{p.provider}</div>
              <div className="mt-2 flex flex-wrap gap-2">
                {p.models.map((m) => (
                  <span
                    key={m}
                    className="rounded-md border border-border bg-surface px-2 py-1 font-mono text-xs"
                  >
                    {m}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function ThemePicker({
  value,
  onChange,
}: {
  value: Theme;
  onChange: (t: Theme) => void;
}) {
  return (
    <label className="flex items-center gap-2 text-sm text-fg-muted">
      Theme
      <select
        value={value}
        onChange={(e) => onChange(e.target.value as Theme)}
        className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg"
      >
        <option value="system">System</option>
        <option value="light">Light</option>
        <option value="dark">Dark</option>
      </select>
    </label>
  );
}
