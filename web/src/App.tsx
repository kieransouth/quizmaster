import { useEffect, useState } from "react";
import { applyTheme, getStoredTheme, type Theme } from "./theme";

type Health = { status: string; version: string };

export default function App() {
  const [theme, setTheme] = useState<Theme>(getStoredTheme());
  const [health, setHealth] = useState<Health | "loading" | "error">("loading");

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    fetch("/api/health")
      .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
      .then((data: Health) => setHealth(data))
      .catch(() => setHealth("error"));
  }, []);

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <h1 className="text-xl font-semibold tracking-tight">Quizmaster</h1>
          <ThemePicker value={theme} onChange={setTheme} />
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-16">
        <div className="rounded-xl border border-border bg-surface p-8 shadow-sm">
          <h2 className="text-2xl font-semibold">Scaffold ready.</h2>
          <p className="mt-2 text-fg-muted">
            Phase 1 complete. The frontend is talking to the API.
          </p>

          <div className="mt-6 rounded-lg bg-surface-muted p-4 font-mono text-sm">
            <div className="text-fg-muted">GET /api/health</div>
            <div className="mt-2">
              {health === "loading" && "loading…"}
              {health === "error" && (
                <span className="text-red-500">unreachable</span>
              )}
              {typeof health === "object" && (
                <>
                  status: <span className="text-accent">{health.status}</span>
                  <br />
                  version:{" "}
                  <span className="text-accent">{health.version}</span>
                </>
              )}
            </div>
          </div>
        </div>
      </main>
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
