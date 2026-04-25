import { create } from "zustand";
import type { UserInfo } from "./types";

/**
 * Auth state lives in memory only — never localStorage. On reload the
 * accessToken is gone but the httpOnly refresh cookie is still on the
 * domain, so a /api/auth/refresh on bootstrap rehydrates the session.
 */
interface AuthState {
  accessToken: string | null;
  user: UserInfo | null;
  /** True once we've attempted the bootstrap refresh, regardless of outcome. */
  bootstrapped: boolean;
  /**
   * Server-driven flag (from /auth/config). Defaults to true so the UI doesn't
   * flicker "registration closed" before bootstrap fetches the real value.
   */
  registrationEnabled: boolean;

  setAuth: (accessToken: string, user: UserInfo) => void;
  clearAuth: () => void;
  setBootstrapped: () => void;
  setRegistrationEnabled: (enabled: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  bootstrapped: false,
  registrationEnabled: true,
  setAuth: (accessToken, user) => set({ accessToken, user }),
  clearAuth: () => set({ accessToken: null, user: null }),
  setBootstrapped: () => set({ bootstrapped: true }),
  setRegistrationEnabled: (enabled) => set({ registrationEnabled: enabled }),
}));
