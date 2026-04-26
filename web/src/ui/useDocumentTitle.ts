import { useEffect } from "react";

/**
 * Sets `document.title` to `<title> — Quizmaster` for the lifetime of
 * the calling component. Pass `null` (or empty) to keep the bare
 * `Quizmaster` title — used by the public landing page so the tab
 * label matches the brand without a redundant "Quizmaster — Quizmaster".
 */
export function useDocumentTitle(title: string | null | undefined): void {
  useEffect(() => {
    document.title = title ? `${title} — Quizmaster` : "Quizmaster";
  }, [title]);
}
