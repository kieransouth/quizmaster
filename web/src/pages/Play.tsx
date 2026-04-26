import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  completeSession,
  getSession,
  gradeAnswer,
  recordAnswer,
  reveal,
  type SessionDto,
  type SessionQuestionDto,
} from "../sessions/api";
import { ThemePicker } from "../ui/ThemePicker";
import { useToast } from "../ui/toast";

export default function Play() {
  const { id } = useParams<{ id: string }>();
  const [session, setSession] = useState<SessionDto | null | "loading" | "notfound">("loading");

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    (async () => {
      try {
        const s = await getSession(id);
        if (!cancelled) setSession(s ?? "notfound");
      } catch {
        if (!cancelled) setSession("notfound");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  if (session === "loading") return <Centered>loading…</Centered>;
  if (session === "notfound" || !session) {
    return (
      <Centered>
        <p>Session not found.</p>
        <Link to="/" className="mt-3 text-accent underline">Back to dashboard</Link>
      </Centered>
    );
  }

  if (session.status === "InProgress") {
    return <Slideshow session={session} onChange={setSession} />;
  }
  if (session.status === "AwaitingReveal") {
    return <GradingScreen session={session} onChange={setSession} />;
  }
  return <CompletionScreen session={session} />;
}

// ============================================================
// Slideshow (InProgress)
// ============================================================

function Slideshow({
  session,
  onChange,
}: {
  session: SessionDto;
  onChange: (s: SessionDto) => void;
}) {
  const { push } = useToast();
  const [idx, setIdx] = useState(0);
  const q = session.questions[idx];
  const last = idx === session.questions.length - 1;

  const [draft, setDraft] = useState(q.answer.answerText);
  const [showHelp, setShowHelp] = useState(false);
  const [busy, setBusy] = useState<"next" | "prev" | "reveal" | null>(null);

  // Sync local draft when navigating to a different question.
  useEffect(() => {
    setDraft(q.answer.answerText);
  }, [q.id, q.answer.answerText]);

  const persistAndAdvance = useCallback(async (direction: "next" | "prev" | "reveal") => {
    if (busy) return; // ignore double-clicks while a save is in flight
    setBusy(direction);
    try {
      // Save the current draft if it differs.
      if (draft !== q.answer.answerText) {
        try {
          const updated = await recordAnswer(session.id, q.id, draft);
          onChange(updated);
        } catch (e) {
          push(e instanceof Error ? e.message : "Save failed", "error");
          return;
        }
      }

      if (direction === "prev" && idx > 0) setIdx(idx - 1);
      if (direction === "next" && !last) setIdx(idx + 1);
      if (direction === "reveal" && last) {
        try {
          const updated = await reveal(session.id);
          onChange(updated);
        } catch (e) {
          push(e instanceof Error ? e.message : "Reveal failed", "error");
        }
      }
    } finally {
      setBusy(null);
    }
  }, [busy, draft, q, idx, last, session.id, onChange, push]);

  // Global keyboard handlers.
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      // Don't intercept when user is interacting with form controls beyond Enter.
      const target = e.target as HTMLElement;
      const inEditable =
        target.tagName === "TEXTAREA" ||
        (target.tagName === "INPUT" && (target as HTMLInputElement).type === "text");

      if (e.key === "?" || (e.key === "/" && e.shiftKey)) {
        e.preventDefault();
        setShowHelp((v) => !v);
        return;
      }
      if (e.key === "Escape" && showHelp) {
        e.preventDefault();
        setShowHelp(false);
        return;
      }
      if (showHelp) return; // pause shortcuts while overlay is open

      // MC: digit keys 1..N pick the option at that index.
      if (
        q.type === "MultipleChoice" &&
        q.options &&
        !inEditable &&
        /^[1-9]$/.test(e.key)
      ) {
        const i = parseInt(e.key, 10) - 1;
        if (i < q.options.length) {
          e.preventDefault();
          setDraft(q.options[i]);
          return;
        }
      }

      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        if (last) void persistAndAdvance("reveal");
        else void persistAndAdvance("next");
      } else if (e.key === "ArrowRight" && !inEditable) {
        e.preventDefault();
        if (!last) void persistAndAdvance("next");
      } else if (e.key === "ArrowLeft" && !inEditable) {
        e.preventDefault();
        if (idx > 0) void persistAndAdvance("prev");
      } else if (e.key === "Escape" && last) {
        e.preventDefault();
        void persistAndAdvance("reveal");
      }
    }
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [idx, last, draft, q, persistAndAdvance, showHelp]);

  return (
    <div className="flex min-h-screen flex-col bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4 text-sm">
          <div className="text-fg-muted">
            <Link to={`/quizzes/${session.quizId}`}>← Quiz</Link>
            <span className="mx-2">·</span>
            <span className="text-fg">{session.quizTitle}</span>
          </div>
          <div className="flex items-center gap-4 text-fg-muted">
            <button
              type="button"
              onClick={() => setShowHelp(true)}
              title="Show keyboard shortcuts (?)"
              className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg-muted hover:text-fg"
            >
              ?
            </button>
            <ThemePicker compact />
            <span>
              Question <span className="text-fg">{idx + 1}</span> / {session.questions.length}
            </span>
          </div>
        </div>
      </header>

      <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col justify-center px-6 py-12">
        <h2 className="text-3xl font-semibold leading-snug">{q.text}</h2>

        {q.type === "MultipleChoice" && q.options ? (
          <div className="mt-8 space-y-2">
            {q.options.map((opt, i) => (
              <label
                key={opt}
                className={
                  "flex cursor-pointer items-center gap-3 rounded-md border bg-surface px-4 py-3 transition " +
                  (draft === opt ? "border-accent" : "border-border hover:bg-surface-muted")
                }
              >
                <input
                  type="radio"
                  name="mc"
                  value={opt}
                  checked={draft === opt}
                  onChange={() => setDraft(opt)}
                />
                <kbd className="rounded border border-border bg-surface-muted px-1.5 py-0.5 font-mono text-xs text-fg-muted">
                  {i + 1}
                </kbd>
                <span>{opt}</span>
              </label>
            ))}
          </div>
        ) : (
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={3}
            placeholder="Team answer…"
            autoFocus
            className="mt-8 w-full rounded-md border border-border bg-surface px-4 py-3 text-lg text-fg outline-none focus:border-accent"
          />
        )}

        <p className="mt-6 text-xs text-fg-muted">
          {q.type === "MultipleChoice" ? "1–9 to pick · " : ""}Enter to{" "}
          {last ? "reveal answers" : "save + next"}; ←/→ to navigate.
        </p>
      </main>

      <footer className="border-t border-border bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4 px-6 py-4">
          <button
            type="button"
            onClick={() => persistAndAdvance("prev")}
            disabled={idx === 0 || busy !== null}
            className="rounded-md border border-border bg-surface-muted px-4 py-2 text-sm text-fg-muted hover:text-fg disabled:opacity-30"
          >
            {busy === "prev" ? <SpinnerLabel label="Saving" /> : "← Previous"}
          </button>
          {last ? (
            <button
              type="button"
              onClick={() => persistAndAdvance("reveal")}
              disabled={busy !== null}
              className="flex items-center justify-center rounded-md bg-accent px-6 py-2 font-medium text-accent-fg disabled:opacity-60"
            >
              {busy === "reveal" ? <SpinnerLabel label="Revealing" /> : "Reveal answers"}
            </button>
          ) : (
            <button
              type="button"
              onClick={() => persistAndAdvance("next")}
              disabled={busy !== null}
              className="flex items-center justify-center rounded-md bg-accent px-6 py-2 font-medium text-accent-fg disabled:opacity-60"
            >
              {busy === "next" ? <SpinnerLabel label="Saving" /> : "Next →"}
            </button>
          )}
        </div>
      </footer>

      {showHelp && <KeyboardHelpOverlay last={last} onClose={() => setShowHelp(false)} />}
    </div>
  );
}

function SpinnerLabel({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center gap-2">
      <svg
        viewBox="0 0 24 24"
        className="h-4 w-4 animate-spin"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
      >
        <circle cx="12" cy="12" r="9" opacity="0.25" />
        <path d="M21 12a9 9 0 0 0-9-9" />
      </svg>
      <span>{label}…</span>
    </span>
  );
}

function KeyboardHelpOverlay({ last, onClose }: { last: boolean; onClose: () => void }) {
  return (
    <div
      onClick={onClose}
      className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 px-4"
    >
      <div
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-md rounded-xl border border-border bg-surface p-6 shadow-xl"
      >
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">Keyboard shortcuts</h3>
          <button
            type="button"
            onClick={onClose}
            aria-label="close"
            className="text-fg-muted hover:text-fg"
          >
            ×
          </button>
        </div>
        <dl className="mt-4 space-y-2 text-sm">
          <Shortcut keys={["1", "…", "9"]} desc="pick a multiple-choice option" />
          <Shortcut keys={["Enter"]} desc={last ? "save and reveal answers" : "save and advance"} />
          <Shortcut keys={["→"]} desc="next question" />
          <Shortcut keys={["←"]} desc="previous question" />
          <Shortcut keys={["Esc"]} desc="reveal (only on the final question)" />
          <Shortcut keys={["?"]} desc="toggle this help" />
        </dl>
      </div>
    </div>
  );
}

function Shortcut({ keys, desc }: { keys: string[]; desc: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-fg-muted">{desc}</span>
      <span className="flex gap-1">
        {keys.map((k) => (
          <kbd
            key={k}
            className="rounded border border-border bg-surface-muted px-2 py-0.5 font-mono text-xs"
          >
            {k}
          </kbd>
        ))}
      </span>
    </div>
  );
}

// ============================================================
// Grading screen (AwaitingReveal)
// ============================================================

function GradingScreen({
  session,
  onChange,
}: {
  session: SessionDto;
  onChange: (s: SessionDto) => void;
}) {
  const { push } = useToast();
  const [showTotal, setShowTotal] = useState(true);
  const allGraded = session.questions.every((q) => q.answer.isCorrect !== null);

  // Global keyboard: T to toggle running total.
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      const target = e.target as HTMLElement;
      const inEditable = target.tagName === "TEXTAREA" || target.tagName === "INPUT";
      if (inEditable) return;
      if (e.key === "t" || e.key === "T") {
        e.preventDefault();
        setShowTotal((v) => !v);
      }
    }
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  async function setGrade(q: SessionQuestionDto, isCorrect: boolean, points: number, note?: string) {
    try {
      const updated = await gradeAnswer(session.id, q.id, isCorrect, points, note);
      onChange(updated);
    } catch (e) {
      push(e instanceof Error ? e.message : "Grade failed", "error");
    }
  }

  async function onComplete() {
    try {
      const updated = await completeSession(session.id);
      onChange(updated);
      push("Session graded.", "success");
    } catch (e) {
      push(e instanceof Error ? e.message : "Complete failed", "error");
    }
  }

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4 text-sm">
          <div className="text-fg-muted">
            <Link to={`/quizzes/${session.quizId}`}>← Quiz</Link>
            <span className="mx-2">·</span>
            <span className="text-fg">{session.quizTitle}</span>
            <span className="mx-2">·</span>
            <span>Reveal &amp; grade</span>
          </div>
          <div className="flex items-center gap-3">
            <ThemePicker compact />
            <button
              type="button"
              onClick={() => setShowTotal((v) => !v)}
              className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg-muted hover:text-fg"
            >
              {showTotal ? "Hide total (T)" : "Show total (T)"}
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl space-y-3 px-6 py-8">
        {session.questions.map((q, i) => (
          <GradeRow key={q.id} index={i} q={q} onSet={setGrade} />
        ))}

        <div className="sticky bottom-0 mt-4 flex items-center justify-between rounded-md border border-border bg-surface p-4">
          <div className="text-sm">
            {showTotal ? (
              <>
                Score:{" "}
                <span className="text-lg font-medium text-fg">
                  {session.score.toFixed(1)} / {session.maxScore}
                </span>
              </>
            ) : (
              <span className="text-fg-muted">Score hidden (press T to show)</span>
            )}
          </div>
          <button
            type="button"
            disabled={!allGraded}
            onClick={onComplete}
            className="rounded-md bg-accent px-6 py-2 font-medium text-accent-fg disabled:opacity-40"
          >
            {allGraded ? "Save results" : "Grade every question first"}
          </button>
        </div>
      </main>
    </div>
  );
}

function GradeRow({
  index,
  q,
  onSet,
}: {
  index: number;
  q: SessionQuestionDto;
  onSet: (q: SessionQuestionDto, isCorrect: boolean, points: number, note?: string) => Promise<void>;
}) {
  const [partial, setPartial] = useState<string>(
    q.answer.pointsAwarded > 0 && q.answer.pointsAwarded < 1
      ? q.answer.pointsAwarded.toString()
      : "0.5",
  );
  const teamGuessed = q.answer.answerText.trim().length > 0;

  return (
    <div className="rounded-md border border-border bg-surface p-4">
      <div className="text-xs text-fg-muted">
        {index + 1}. {q.type === "MultipleChoice" ? "Multiple choice" : "Free text"}
      </div>
      <p className="mt-1 text-base font-medium">{q.text}</p>

      <div className="mt-3 grid grid-cols-2 gap-3 text-sm">
        <div>
          <div className="text-fg-muted">Team's answer</div>
          <div
            className={
              "mt-1 rounded-md bg-surface-muted px-3 py-2 font-mono " +
              (teamGuessed ? "text-fg" : "italic text-fg-muted")
            }
          >
            {teamGuessed ? q.answer.answerText : "(no answer)"}
          </div>
        </div>
        <div>
          <div className="text-fg-muted">Correct</div>
          <div className="mt-1 rounded-md bg-accent/10 px-3 py-2 font-mono text-fg">
            {q.correctAnswer}
          </div>
        </div>
      </div>

      {q.explanation && (
        <p className="mt-2 text-xs italic text-fg-muted">{q.explanation}</p>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-2 text-sm">
        {q.type === "MultipleChoice" ? (
          <span className="rounded-md bg-surface-muted px-3 py-1 text-fg-muted">
            Auto-graded:{" "}
            <span className={q.answer.isCorrect ? "text-green-500" : "text-red-500"}>
              {q.answer.isCorrect ? "correct (1.0)" : "incorrect (0.0)"}
            </span>
          </span>
        ) : (
          <>
            <button
              type="button"
              onClick={() => onSet(q, false, 0)}
              className={
                "rounded-md border px-3 py-1 " +
                (q.answer.isCorrect === false && q.answer.pointsAwarded === 0
                  ? "border-red-500 bg-red-500/20 text-red-700 dark:text-red-300"
                  : "border-border text-fg-muted hover:bg-surface-muted")
              }
            >
              ✗ wrong
            </button>
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => onSet(q, true, parseFloat(partial) || 0.5)}
                className={
                  "rounded-md border px-3 py-1 " +
                  (q.answer.isCorrect === true && q.answer.pointsAwarded > 0 && q.answer.pointsAwarded < 1
                    ? "border-yellow-500 bg-yellow-500/20"
                    : "border-border text-fg-muted hover:bg-surface-muted")
                }
              >
                partial
              </button>
              <input
                type="number"
                min={0}
                max={1}
                step={0.1}
                value={partial}
                onChange={(e) => setPartial(e.target.value)}
                className="w-16 rounded-md border border-border bg-surface-muted px-2 py-1 text-fg"
              />
            </div>
            <button
              type="button"
              onClick={() => onSet(q, true, 1)}
              className={
                "rounded-md border px-3 py-1 " +
                (q.answer.isCorrect === true && q.answer.pointsAwarded === 1
                  ? "border-green-500 bg-green-500/20 text-green-700 dark:text-green-300"
                  : "border-border text-fg-muted hover:bg-surface-muted")
              }
            >
              ✓ correct
            </button>
          </>
        )}
      </div>
    </div>
  );
}

// ============================================================
// Completion screen (Graded)
// ============================================================

function CompletionScreen({ session }: { session: SessionDto }) {
  const { push } = useToast();
  const [copied, setCopied] = useState(false);
  const shareUrl = `${window.location.origin}/share/${session.publicShareToken}`;
  const pct = session.maxScore > 0 ? Math.round((session.score / session.maxScore) * 100) : 0;

  async function copyShare() {
    try {
      await navigator.clipboard.writeText(shareUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      push(`Copy failed — link is: ${shareUrl}`, "info");
    }
  }

  return (
    <div className="min-h-screen bg-bg">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4 text-sm">
          <div className="text-fg-muted">
            <Link to="/">← Dashboard</Link>
            <span className="mx-2">·</span>
            <span className="text-fg">{session.quizTitle}</span>
          </div>
          <div className="flex items-center gap-3">
            <ThemePicker compact />
            <span className="text-fg-muted">Done</span>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-3xl space-y-6 px-6 py-12">
        <div className="rounded-xl border border-border bg-surface p-8 text-center">
          <p className="text-sm uppercase tracking-wide text-fg-muted">Final score</p>
          <p className="mt-2 text-5xl font-semibold">
            {session.score.toFixed(1)}{" "}
            <span className="text-fg-muted">/ {session.maxScore}</span>
          </p>
          <p className="mt-2 text-lg text-fg-muted">{pct}%</p>
        </div>

        <div className="rounded-md border border-border bg-surface p-4">
          <p className="text-sm text-fg-muted">
            Public share link — anyone can view the score without an account.
          </p>
          <div className="mt-2 flex items-center gap-2">
            <a
              href={`/share/${session.publicShareToken}`}
              target="_blank"
              rel="noreferrer"
              className="flex-1 truncate rounded-md bg-surface-muted px-3 py-2 font-mono text-xs hover:underline"
            >
              {shareUrl}
            </a>
            <button
              type="button"
              onClick={copyShare}
              className="rounded-md border border-border bg-surface-muted px-3 py-2 text-sm hover:bg-surface"
            >
              {copied ? "copied" : "copy"}
            </button>
          </div>
        </div>

        <ul className="space-y-2">
          {session.questions.map((q, i) => (
            <li key={q.id} className="rounded-md border border-border bg-surface p-4 text-sm">
              <div className="text-xs text-fg-muted">{i + 1}. {q.type}</div>
              <p className="mt-1 font-medium">{q.text}</p>
              <div className="mt-2 grid grid-cols-2 gap-3">
                <div>
                  <div className="text-xs text-fg-muted">Team</div>
                  <div className={"font-mono " + (q.answer.isCorrect ? "text-green-500" : "text-red-500")}>
                    {q.answer.answerText || "(no answer)"}
                  </div>
                </div>
                <div>
                  <div className="text-xs text-fg-muted">Correct</div>
                  <div className="font-mono text-fg">{q.correctAnswer}</div>
                </div>
              </div>
              <div className="mt-2 text-xs text-fg-muted">
                {q.answer.pointsAwarded.toFixed(1)} pt
                {q.answer.gradingNote ? ` · "${q.answer.gradingNote}"` : ""}
              </div>
            </li>
          ))}
        </ul>
      </main>
    </div>
  );
}

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-2 text-fg-muted">
      {children}
    </div>
  );
}
