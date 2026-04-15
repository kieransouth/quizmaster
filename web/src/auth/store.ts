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

  setAuth: (accessToken: string, user: UserInfo) => void;
  clearAuth: () => void;
  setBootstrapped: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  bootstrapped: false,
  setAuth: (accessToken, user) => set({ accessToken, user }),
  clearAuth: () => set({ accessToken: null, user: null }),
  setBootstrapped: () => set({ bootstrapped: true }),
}));
