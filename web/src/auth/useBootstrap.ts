import { useEffect } from "react";
import { tryRefresh } from "../api/client";
import { useAuthStore } from "./store";

/**
 * On app start, attempt to rehydrate the session via the refresh cookie and
 * fetch /auth/config so the login/register pages know whether registration
 * is enabled. Sets `bootstrapped` to true once the attempts have resolved,
 * so ProtectedRoute can avoid flashing the login page.
 */
export function useBootstrap() {
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  const setBootstrapped = useAuthStore((s) => s.setBootstrapped);
  const setRegistrationEnabled = useAuthStore((s) => s.setRegistrationEnabled);

  useEffect(() => {
    if (bootstrapped) return;
    let cancelled = false;
    (async () => {
      await Promise.all([
        tryRefresh(),
        (async () => {
          try {
            const res = await fetch("/api/auth/config", { credentials: "include" });
            if (!res.ok) return;
            const cfg = (await res.json()) as { registrationEnabled: boolean };
            if (!cancelled) setRegistrationEnabled(cfg.registrationEnabled);
          } catch {
            // Leave default (true) — don't block bootstrap on config errors.
          }
        })(),
      ]);
      if (!cancelled) setBootstrapped();
    })();
    return () => {
      cancelled = true;
    };
  }, [bootstrapped, setBootstrapped, setRegistrationEnabled]);
}
