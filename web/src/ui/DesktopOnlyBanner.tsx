import { useState } from "react";

const STORAGE_KEY = "qm-desktop-banner-dismissed";

/**
 * Shows a tiny banner on screens narrower than the lg breakpoint
 * suggesting that hosting/play works best on desktop. Dismissable —
 * the choice persists in localStorage.
 */
export function DesktopOnlyBanner() {
  const [dismissed, setDismissed] = useState(
    () => typeof window !== "undefined" && window.localStorage.getItem(STORAGE_KEY) === "1",
  );
  if (dismissed) return null;

  function dismiss() {
    window.localStorage.setItem(STORAGE_KEY, "1");
    setDismissed(true);
  }

  return (
    <div className="lg:hidden">
      <div className="flex items-center justify-between gap-3 border-b border-border bg-surface-muted px-4 py-2 text-xs text-fg-muted">
        <span>
          Quizmaster is optimised for desktop — the play and edit screens
          assume a wider window.
        </span>
        <button
          type="button"
          onClick={dismiss}
          aria-label="dismiss"
          className="text-fg-muted hover:text-fg"
        >
          ×
        </button>
      </div>
    </div>
  );
}
