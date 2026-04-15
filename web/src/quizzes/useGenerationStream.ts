import { useCallback, useRef, useState } from "react";
import { tryRefresh } from "../api/client";
import { useAuthStore } from "../auth/store";
import type { GenerationEvent } from "./types";

interface State {
  events: GenerationEvent[];
  running: boolean;
}

/**
 * Streams Server-Sent Events from a POST endpoint and yields parsed
 * GenerationEvent objects. Uses fetch + ReadableStream because the
 * native EventSource API doesn't support POST bodies.
 */
export function useGenerationStream() {
  const [state, setState] = useState<State>({ events: [], running: false });
  const abortRef = useRef<AbortController | null>(null);

  const start = useCallback(async (url: string, body: unknown) => {
    if (abortRef.current) return; // loading lock — debounce double-clicks

    setState({ events: [], running: true });
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      let token = useAuthStore.getState().accessToken;
      let response = await fetchSse(url, body, token, controller.signal);

      // Single auto-refresh attempt on 401, mirroring apiFetch.
      if (response.status === 401) {
        const refreshed = await tryRefresh();
        if (refreshed) {
          token = refreshed.accessToken;
          response = await fetchSse(url, body, token, controller.signal);
        }
      }

      if (!response.ok || !response.body) {
        appendEvent(setState, {
          type: "error",
          message: `HTTP ${response.status}`,
          retryable: response.status >= 500,
        });
        return;
      }

      for await (const evt of parseSseStream(response.body)) {
        appendEvent(setState, evt);
        if (evt.type === "done" || evt.type === "error") break;
      }
    } catch (e) {
      if (controller.signal.aborted) return; // user-initiated cancel; silent
      appendEvent(setState, {
        type: "error",
        message: e instanceof Error ? e.message : "Unknown error",
        retryable: true,
      });
    } finally {
      abortRef.current = null;
      setState((s) => ({ ...s, running: false }));
    }
  }, []);

  const abort = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setState((s) => ({ ...s, running: false }));
  }, []);

  const reset = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
    setState({ events: [], running: false });
  }, []);

  return { ...state, start, abort, reset };
}

function appendEvent(setState: React.Dispatch<React.SetStateAction<State>>, evt: GenerationEvent) {
  setState((s) => ({ ...s, events: [...s.events, evt] }));
}

async function fetchSse(
  url: string,
  body: unknown,
  token: string | null,
  signal: AbortSignal,
): Promise<Response> {
  return fetch(url, {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      Accept: "text/event-stream",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
    signal,
  });
}

async function* parseSseStream(
  stream: ReadableStream<Uint8Array>,
): AsyncGenerator<GenerationEvent> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      let boundary: number;
      while ((boundary = buffer.indexOf("\n\n")) >= 0) {
        const frame = buffer.slice(0, boundary);
        buffer = buffer.slice(boundary + 2);
        const match = frame.match(/^data:\s?(.*)$/);
        if (!match) continue;
        try {
          yield JSON.parse(match[1]) as GenerationEvent;
        } catch {
          // Bad frame — ignore.
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}
