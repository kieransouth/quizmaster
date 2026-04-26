import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

export type ToastKind = "info" | "success" | "error";

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

interface ToastContextValue {
  push: (message: string, kind?: ToastKind) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

// eslint-disable-next-line react-refresh/only-export-components
export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within <ToastProvider>");
  return ctx;
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const push = useCallback((message: string, kind: ToastKind = "info") => {
    setToasts((current) => [...current, { id: Date.now() + Math.random(), kind, message }]);
  }, []);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  return (
    <ToastContext.Provider value={{ push }}>
      {children}
      <div className="pointer-events-none fixed right-4 top-4 z-50 flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
        {toasts.map((t) => (
          <ToastItem key={t.id} toast={t} onDismiss={() => dismiss(t.id)} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastItem({ toast, onDismiss }: { toast: Toast; onDismiss: () => void }) {
  // Auto-dismiss after 5 seconds.
  useEffect(() => {
    const t = setTimeout(onDismiss, 5000);
    return () => clearTimeout(t);
  }, [onDismiss]);

  const palette =
    toast.kind === "error"
      ? "border-red-500/50 bg-red-500/10 text-red-700 dark:text-red-300"
      : toast.kind === "success"
        ? "border-green-500/50 bg-green-500/10 text-green-700 dark:text-green-300"
        : "border-border bg-surface text-fg";

  return (
    <div
      role="status"
      className={`pointer-events-auto flex items-start gap-3 rounded-md border p-3 text-sm shadow-md ${palette}`}
    >
      <span className="flex-1">{toast.message}</span>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="dismiss"
        className="text-fg-muted hover:text-fg"
      >
        ×
      </button>
    </div>
  );
}
