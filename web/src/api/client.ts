import { useAuthStore } from "../auth/store";
import type { TokenPair } from "../auth/types";

/**
 * Wraps fetch with two behaviours:
 *  1. Adds Authorization: Bearer <accessToken> if we have one
 *  2. On 401, calls /api/auth/refresh ONCE, updates the store, and retries
 *
 * The refresh cookie is httpOnly + scoped to /api/auth so the browser sends
 * it automatically on the refresh call. If refresh also returns 401, we
 * clear auth so ProtectedRoute redirects to /login.
 */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const doFetch = (token: string | null) => {
    const headers = new Headers(init.headers);
    if (token) headers.set("Authorization", `Bearer ${token}`);
    if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
    return fetch(input, { ...init, headers, credentials: "include" });
  };

  const initialToken = useAuthStore.getState().accessToken;
  let response = await doFetch(initialToken);

  if (response.status !== 401) return response;

  // Don't try to refresh while calling refresh itself.
  if (input.endsWith("/api/auth/refresh")) return response;

  const refreshed = await tryRefresh();
  if (!refreshed) {
    useAuthStore.getState().clearAuth();
    return response;
  }
  response = await doFetch(refreshed.accessToken);
  return response;
}

let inflightRefresh: Promise<TokenPair | null> | null = null;

export function tryRefresh(): Promise<TokenPair | null> {
  if (!inflightRefresh) {
    inflightRefresh = (async () => {
      try {
        const res = await fetch("/api/auth/refresh", {
          method: "POST",
          credentials: "include",
        });
        if (!res.ok) return null;
        const pair = (await res.json()) as TokenPair;
        useAuthStore.getState().setAuth(pair.accessToken, pair.user);
        return pair;
      } catch {
        return null;
      } finally {
        // Allow another refresh attempt later
        setTimeout(() => {
          inflightRefresh = null;
        }, 0);
      }
    })();
  }
  return inflightRefresh;
}
