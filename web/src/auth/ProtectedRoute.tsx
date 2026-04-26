import type { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { LoadingShell } from "../ui/LoadingShell";
import { useAuthStore } from "./store";

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  const user = useAuthStore((s) => s.user);

  if (!bootstrapped) return <LoadingShell />;
  if (!user) return <Navigate to="/login" replace />;

  return <>{children}</>;
}
