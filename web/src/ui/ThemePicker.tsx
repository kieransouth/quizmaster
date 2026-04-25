import { useEffect, useState } from "react";
import { applyTheme, getStoredTheme, type Theme } from "../theme";

/**
 * Reusable theme switcher. Manages its own state — bootstrap-on-mount
 * applies whatever's persisted in localStorage, and changes are pushed
 * straight to documentElement[data-theme] via applyTheme.
 */
export function ThemePicker({ compact = false }: { compact?: boolean }) {
  const [theme, setTheme] = useState<Theme>(getStoredTheme());

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  return (
    <label className="flex items-center gap-2 text-sm text-fg-muted">
      {!compact && <span>Theme</span>}
      <select
        value={theme}
        onChange={(e) => setTheme(e.target.value as Theme)}
        className="rounded-md border border-border bg-surface-muted px-2 py-1 text-fg"
        aria-label="Theme"
      >
        <option value="system">System</option>
        <option value="light">Light</option>
        <option value="dark">Dark</option>
      </select>
    </label>
  );
}
