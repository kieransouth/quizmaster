import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { useAuthStore } from "./store";

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  const user = useAuthStore((s) => s.user);

  if (!bootstrapped) {
    return (
      <div className="flex min-h-screen items-center justify-center text-fg-muted">
        Loading…
      </div>
    );
  }
  if (!user) return <Navigate to="/login" replace />;

  return <>{children}</>;
}
