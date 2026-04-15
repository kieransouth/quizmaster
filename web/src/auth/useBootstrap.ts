import { useEffect } from "react";
import { tryRefresh } from "../api/client";
import { useAuthStore } from "./store";

/**
 * On app start, attempt to rehydrate the session via the refresh cookie.
 * Sets `bootstrapped` to true once the attempt has resolved, so
 * ProtectedRoute can avoid flashing the login page.
 */
export function useBootstrap() {
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  const setBootstrapped = useAuthStore((s) => s.setBootstrapped);

  useEffect(() => {
    if (bootstrapped) return;
    let cancelled = false;
    (async () => {
      await tryRefresh();
      if (!cancelled) setBootstrapped();
    })();
    return () => {
      cancelled = true;
    };
  }, [bootstrapped, setBootstrapped]);
}
