import { useEffect, useState } from "react";
import { apiFetch } from "../api/client";
import type { AiProvidersResponse } from "./types";

type State =
  | { kind: "loading" }
  | { kind: "ok"; data: AiProvidersResponse }
  | { kind: "error"; status: number };

export function useProviders(): State {
  const [state, setState] = useState<State>({ kind: "loading" });

  useEffect(() => {
    let cancelled = false;
    apiFetch("/api/ai/providers")
      .then(async (res) => {
        if (cancelled) return;
        if (!res.ok) {
          setState({ kind: "error", status: res.status });
          return;
        }
        const data = (await res.json()) as AiProvidersResponse;
        setState({ kind: "ok", data });
      })
      .catch(() => {
        if (!cancelled) setState({ kind: "error", status: 0 });
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
