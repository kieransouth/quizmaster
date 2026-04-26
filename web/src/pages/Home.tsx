import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuthStore } from "../auth/store";
import { listMyQuizzes, type QuizSummaryDto } from "../quizzes/api";
import { GithubLink } from "../ui/GithubLink";
import { ThemePicker } from "../ui/ThemePicker";
import { useDocumentTitle } from "../ui/useDocumentTitle";

export default function Home() {
  useDocumentTitle("Dashboard");
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const clearAuth = useAuthStore((s) => s.clearAuth);
  const [quizzes, setQuizzes] = useState<QuizSummaryDto[] | "loading" | "error">("loading");

  useEffect(() => {
    let cancelled = false;
    listMyQuizzes()
      .then((data) => { if (!cancelled) setQuizzes(data); })
      .catch(() => { if (!cancelled) setQuizzes("error"); });
    return () => { cancelled = true; };
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
            <Link to="/settings" className="text-fg-muted hover:text-fg">
              Settings
            </Link>
            <ThemePicker />
            <GithubLink />
            <button
              onClick={onLogout}
              className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg hover:bg-surface"
            >
              Logout
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-6 px-6 py-10">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-2xl font-semibold">Your quizzes</h2>
            <p className="mt-1 text-sm text-fg-muted">
              Saved drafts. Pick one to edit, or generate a new one.
            </p>
          </div>
          <Link
            to="/quizzes/new"
            className="rounded-md bg-accent px-4 py-2 font-medium text-accent-fg"
          >
            New quiz
          </Link>
        </div>

        {quizzes === "loading" && <QuizListSkeleton />}

        {quizzes === "error" && (
          <div className="rounded-md border border-red-500/40 bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-300">
            Failed to load your quizzes.
          </div>
        )}

        {Array.isArray(quizzes) && quizzes.length === 0 && (
          <EmptyDashboard />
        )}

        {Array.isArray(quizzes) && quizzes.length > 0 && (
          <ul className="space-y-2">
            {quizzes.map((q) => (
              <li key={q.id}>
                <Link
                  to={`/quizzes/${q.id}`}
                  className="flex items-center justify-between rounded-md border border-border bg-surface p-4 hover:bg-surface-muted"
                >
                  <div>
                    <div className="font-medium">{q.title}</div>
                    <div className="mt-1 text-xs text-fg-muted">
                      {q.questionCount} question{q.questionCount === 1 ? "" : "s"} ·{" "}
                      {q.source} · {q.providerUsed}/{q.modelUsed} ·{" "}
                      {new Date(q.createdAt).toLocaleString()}
                    </div>
                  </div>
                  <span className="text-fg-muted">→</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}

function QuizListSkeleton() {
  return (
    <ul className="space-y-2">
      {[0, 1, 2].map((i) => (
        <li
          key={i}
          className="flex items-center justify-between rounded-md border border-border bg-surface p-4"
        >
          <div className="flex-1 space-y-2">
            <div className="h-4 w-1/3 animate-pulse rounded bg-surface-muted" />
            <div className="h-3 w-2/3 animate-pulse rounded bg-surface-muted" />
          </div>
        </li>
      ))}
    </ul>
  );
}

function EmptyDashboard() {
  return (
    <div className="rounded-xl border border-dashed border-border bg-surface p-12 text-center">
      <p className="text-3xl">🎯</p>
      <h3 className="mt-4 text-lg font-medium">No quizzes yet</h3>
      <p className="mx-auto mt-2 max-w-sm text-sm text-fg-muted">
        Generate one with AI or import a quiz you found elsewhere — both
        live as drafts you can edit, play, and share.
      </p>
      <Link
        to="/quizzes/new"
        className="mt-5 inline-block rounded-md bg-accent px-4 py-2 font-medium text-accent-fg"
      >
        Make your first quiz
      </Link>
    </div>
  );
}
