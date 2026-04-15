export type Theme = "light" | "dark" | "system";

const STORAGE_KEY = "qm-theme";

export function getStoredTheme(): Theme {
  if (typeof window === "undefined") return "system";
  const v = window.localStorage.getItem(STORAGE_KEY);
  return v === "light" || v === "dark" || v === "system" ? v : "system";
}

export function applyTheme(theme: Theme): void {
  if (typeof document === "undefined") return;
  if (theme === "system") {
    document.documentElement.removeAttribute("data-theme");
  } else {
    document.documentElement.setAttribute("data-theme", theme);
  }
  window.localStorage.setItem(STORAGE_KEY, theme);
}

// Apply stored theme as early as possible to avoid a flash of wrong palette.
export function bootstrapTheme(): void {
  applyTheme(getStoredTheme());
}
