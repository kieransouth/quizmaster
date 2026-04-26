import { Link } from "react-router-dom";
import { useAuthStore } from "../auth/store";
import { GithubLink } from "../ui/GithubLink";
import { ThemePicker } from "../ui/ThemePicker";
import { useDocumentTitle } from "../ui/useDocumentTitle";

/**
 * Public landing page rendered at `/` for signed-out visitors —
 * crawlers, link unfurlers, and first-time visitors arriving from a
 * shared URL. Marketing copy mirrors the README and the GitHub
 * release notes; signed-in users see the dashboard at the same URL.
 */
export default function Landing() {
  // Bare "Quizmaster" tab label — the page is itself the brand intro.
  useDocumentTitle(null);

  const registrationEnabled = useAuthStore((s) => s.registrationEnabled);

  return (
    <div className="min-h-full">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <h1 className="text-xl font-semibold tracking-tight">Quizmaster</h1>
          <div className="flex items-center gap-3 text-sm">
            <ThemePicker />
            <GithubLink />
            <Link
              to="/login"
              className="rounded-md border border-border bg-surface-muted px-3 py-1 text-fg hover:bg-surface"
            >
              Sign in
            </Link>
            {registrationEnabled && (
              <Link
                to="/register"
                className="rounded-md bg-accent px-3 py-1 font-medium text-accent-fg"
              >
                Create account
              </Link>
            )}
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl space-y-16 px-6 py-16">
        {/* Hero */}
        <section className="text-center">
          <h2 className="text-4xl font-semibold leading-tight tracking-tight sm:text-5xl">
            AI-powered quiz wizard for team trivia nights
          </h2>
          <p className="mx-auto mt-5 max-w-2xl text-lg text-fg-muted">
            Pick your provider, pick your topics, host the quiz from your
            browser. Writing a pub quiz from scratch takes an evening — doing
            it with any LLM takes thirty seconds.
          </p>
          <div className="mt-8 flex items-center justify-center gap-3">
            <Link
              to="/login"
              className="rounded-md bg-accent px-5 py-2.5 font-medium text-accent-fg"
            >
              Sign in
            </Link>
            {registrationEnabled && (
              <Link
                to="/register"
                className="rounded-md border border-border bg-surface px-5 py-2.5 text-fg hover:bg-surface-muted"
              >
                Create account
              </Link>
            )}
          </div>
        </section>

        {/* Features */}
        <section className="grid gap-4 sm:grid-cols-2">
          <Feature
            title="Generate or import"
            body="Stream questions from Ollama, OpenAI, or Anthropic — or paste raw text and let the AI extract each Q+A. Topics interleave so you don't get five-in-a-row from one subject."
          />
          <Feature
            title="BYO AI"
            body="No API keys to plug in? Copy a prompt, run it in any external tool, paste the JSON back. Quizmaster makes zero AI calls of its own — perfect for one-off hosts."
          />
          <Feature
            title="Fact-check anything"
            body="Audit a quiz with a separately-chosen model — or BYO-fact-check with the same paste-the-JSON pattern. Disagreements surface inline so you can fix hallucinations before play."
          />
          <Feature
            title="Host on a screen-share"
            body="Keyboard-first slideshow with 1–9 to pick and Enter to advance. Reveal & grade auto-grades exact-match answers; a public share link lets teams revisit the quiz after."
          />
        </section>

        {/* Hero screenshot */}
        <section>
          <a
            href="https://github.com/kieransouth/quizmaster#it-looks-like"
            target="_blank"
            rel="noopener noreferrer"
            className="block overflow-hidden rounded-xl border border-border bg-surface shadow-sm transition hover:border-accent/40"
          >
            <img
              src="/og-image.png"
              alt="Quizmaster dashboard"
              className="block w-full"
            />
          </a>
          <p className="mt-2 text-center text-xs text-fg-muted">
            More screenshots in the{" "}
            <a
              href="https://github.com/kieransouth/quizmaster#it-looks-like"
              target="_blank"
              rel="noopener noreferrer"
              className="underline"
            >
              README gallery
            </a>
            .
          </p>
        </section>
      </main>

      <footer className="border-t border-border bg-surface">
        <div className="mx-auto flex max-w-5xl flex-col items-center justify-between gap-2 px-6 py-6 text-xs text-fg-muted sm:flex-row">
          <span>
            <a
              href="https://github.com/kieransouth/quizmaster"
              target="_blank"
              rel="noopener noreferrer"
              className="underline"
            >
              Self-host it on GitHub
            </a>
          </span>
          <span>MIT licensed</span>
        </div>
      </footer>
    </div>
  );
}

function Feature({ title, body }: { title: string; body: string }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-6">
      <h3 className="text-base font-medium">{title}</h3>
      <p className="mt-2 text-sm text-fg-muted">{body}</p>
    </div>
  );
}
