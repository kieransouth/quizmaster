import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuthStore } from "../auth/store";
import type { TokenPair } from "../auth/types";
import { AuthShell, Field } from "./Login";

export default function Register() {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, displayName }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        const msg =
          (body?.errors as string[] | undefined)?.[0] ??
          (body?.error as string | undefined) ??
          "Registration failed.";
        setError(msg);
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
    <AuthShell title="Create account">
      <form onSubmit={onSubmit} className="space-y-4">
        <Field label="Display name" value={displayName} onChange={setDisplayName} autoFocus required />
        <Field label="Email" type="email" value={email} onChange={setEmail} required />
        <Field
          label="Password (8+ chars)"
          type="password"
          value={password}
          onChange={setPassword}
          minLength={8}
          required
        />
        {error && <p className="text-sm text-red-500">{error}</p>}
        <button
          type="submit"
          disabled={busy}
          className="w-full rounded-md bg-accent px-4 py-2 font-medium text-accent-fg disabled:opacity-50"
        >
          {busy ? "Creating account…" : "Create account"}
        </button>
      </form>
      <p className="mt-6 text-sm text-fg-muted">
        Already have one?{" "}
        <Link to="/login" className="text-accent underline">
          Sign in
        </Link>
      </p>
    </AuthShell>
  );
}
