import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuthStore } from "../auth/store";
import type { TokenPair } from "../auth/types";
import { useDocumentTitle } from "../ui/useDocumentTitle";

export default function Login() {
  useDocumentTitle("Sign in");
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const registrationEnabled = useAuthStore((s) => s.registrationEnabled);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      if (!res.ok) {
        setError(res.status === 401 ? "Invalid email or password." : "Login failed.");
        return;
      }
      const pair = (await res.json()) as TokenPair;
      setAuth(pair.accessToken, pair.user);
      navigate("/", { replace: true });
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthShell title="Sign in">
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Email" type="email" value={email} onChange={setEmail} autoFocus required />
        <Field label="Password" type="password" value={password} onChange={setPassword} required />
        {error && <p className="text-sm text-red-500">{error}</p>}
        <button
          type="submit"
          disabled={busy}
          className="w-full rounded-md bg-accent px-4 py-2 font-medium text-accent-fg disabled:opacity-50"
        >
          {busy ? "Signing in…" : "Sign in"}
        </button>
      </form>
      {registrationEnabled && (
        <p className="mt-6 text-sm text-fg-muted">
          New?{" "}
          <Link to="/register" className="text-accent underline">
            Create an account
          </Link>
        </p>
      )}
    </AuthShell>
  );
}

function AuthShell({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-md rounded-xl border border-border bg-surface p-8 shadow-sm">
        <h1 className="mb-6 text-2xl font-semibold tracking-tight">{title}</h1>
        {children}
      </div>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  ...rest
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm text-fg-muted">{label}</span>
      <input
        {...rest}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-md border border-border bg-surface-muted px-3 py-2 text-fg outline-none focus:border-accent"
      />
    </label>
  );
}

export { AuthShell, Field };
