/**
 * Centered "Loading…" placeholder used while the app bootstraps the
 * auth session. Shared between `ProtectedRoute` and the `/` root
 * branch so they look identical during the brief flash before the
 * refresh-cookie probe completes.
 */
export function LoadingShell() {
  return (
    <div className="flex min-h-screen items-center justify-center text-fg-muted">
      Loading…
    </div>
  );
}
