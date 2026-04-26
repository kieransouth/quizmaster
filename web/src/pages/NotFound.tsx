import { Link } from "react-router-dom";
import { useDocumentTitle } from "../ui/useDocumentTitle";

/**
 * Soft 404 — replaces the previous silent `<Navigate to="/">`. Catches
 * typo'd share links and old routes from before any URL change.
 */
export default function NotFound() {
  useDocumentTitle("Not found");

  return (
    <div className="flex min-h-screen items-center justify-center px-6 text-center">
      <div className="space-y-3">
        <p className="text-5xl font-semibold tracking-tight">404</p>
        <p className="text-lg text-fg-muted">
          That page isn't here. The link may be wrong, or the page has moved.
        </p>
        <p>
          <Link to="/" className="text-accent underline">
            ← Back to Quizmaster
          </Link>
        </p>
      </div>
    </div>
  );
}
