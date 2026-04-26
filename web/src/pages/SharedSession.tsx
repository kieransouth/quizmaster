import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import {
  getPublicSession,
  type PublicQuestion,
  type PublicSessionSummary,
} from "../sessions/sharedApi";
import { ThemePicker } from "../ui/ThemePicker";

export default function SharedSession() {
  const { token } = useParams<{ token: string }>();
  const [state, setState] = useState<
    | { kind: "loading" }
    | { kind: "notfound" }
    | { kind: "error"; message: string }
    | { kind: "ok"; data: PublicSessionSummary }
  >(() => (token ? { kind: "loading" } : { kind: "notfound" }));

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    (async () => {
      try {
        const data = await getPublicSession(token);
        if (cancelled) return;
        setState(data ? { kind: "ok", data } : { kind: "notfound" });
      } catch (e) {
        if (!cancelled) {
          setState({ kind: "error", message: e instanceof Error ? e.message : "Failed to load" });
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [token]);

  if (state.kind === "loading") {
    return <Centered>loading…</Centered>;
  }
  if (state.kind === "notfound") {
    return (
      <Centered>
        <p>This summary isn't available.</p>
        <p className="mt-1 text-xs text-fg-muted">
          The link may be wrong, or the quiz hasn't been completed yet.
        </p>
      </Centered>
    );
  }
  if (state.kind === "error") {
    return <Centered>{state.message}</Centered>;
  }

  const { data } = state;
  const percent = data.maxScore === 0 ? 0 : Math.round((data.score / data.maxScore) * 100);

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-3xl items-center justify-end px-6 py-3">
          <ThemePicker />
        </div>
      </header>
      <main className="mx-auto max-w-3xl space-y-8 px-6 py-12">
        {/* Score hero */}
        <section className="rounded-2xl border border-border bg-surface p-8 text-center shadow-sm">
          <p className="text-sm uppercase tracking-wide text-fg-muted">{data.quizTitle}</p>
          <p className="mt-4 text-6xl font-semibold tabular-nums">
            {formatScore(data.score)}{" "}
            <span className="text-fg-muted">/ {data.maxScore}</span>
          </p>
          <p className="mt-2 text-3xl font-medium text-accent">{percent}%</p>
          <p className="mt-3 text-xs text-fg-muted">
            Played {new Date(data.completedAt).toLocaleString()}
          </p>

          {data.topics.length > 0 && (
            <div className="mt-5 flex flex-wrap justify-center gap-2">
              {data.topics.map((t) => (
                <span
                  key={t.name}
                  className="rounded-full border border-accent/40 bg-accent/10 px-3 py-1 text-xs font-medium text-accent"
                >
                  {t.name}
                  <span className="ml-1 text-fg-muted">×{t.count}</span>
                </span>
              ))}
            </div>
          )}
        </section>

        {/* Per-question rundown */}
        <section className="space-y-3">
          {data.questions.map((q) => (
            <QuestionRow key={q.order} q={q} />
          ))}
        </section>
      </main>
    </div>
  );
}

function QuestionRow({ q }: { q: PublicQuestion }) {
  const correct = q.isCorrect;
  return (
    <div
      className={
        "rounded-md border p-4 " +
        (correct
          ? "border-green-500/40 bg-green-500/5"
          : "border-red-500/40 bg-red-500/5")
      }
    >
      <div className="flex items-baseline justify-between text-xs text-fg-muted">
        <span>
          Q{q.order + 1} ·{" "}
          <span>{q.type === "MultipleChoice" ? "Multiple choice" : "Free text"}</span>
        </span>
        <span className={correct ? "text-green-700 dark:text-green-300" : "text-red-700 dark:text-red-300"}>
          {correct ? "✓" : "✗"} {formatScore(q.pointsAwarded)} pt
          {q.pointsAwarded === 1 ? "" : "s"}
        </span>
      </div>
      <p className="mt-2 font-medium">{q.text}</p>
      <div className="mt-3 grid grid-cols-1 gap-2 text-sm sm:grid-cols-2">
        <div className="rounded bg-surface-muted p-2">
          <div className="text-xs text-fg-muted">Team answer</div>
          <div className={"mt-1 font-mono " + (q.teamAnswer ? "" : "text-fg-muted/60")}>
            {q.teamAnswer || "(blank)"}
          </div>
        </div>
        <div className="rounded bg-surface-muted p-2">
          <div className="text-xs text-fg-muted">Correct answer</div>
          <div className="mt-1 font-mono text-accent">{q.correctAnswer}</div>
        </div>
      </div>
    </div>
  );
}

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center px-6 text-center text-fg-muted">
      <div className="space-y-2">{children}</div>
    </div>
  );
}

function formatScore(n: number): string {
  // Show whole numbers without trailing .0; partial credit shows one decimal.
  return Number.isInteger(n) ? n.toString() : n.toFixed(1);
}
