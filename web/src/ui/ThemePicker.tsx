import { useEffect, useState } from "react";
import { applyTheme, getStoredTheme, type Theme } from "../theme";

const ORDER: readonly Theme[] = ["light", "dark", "system"] as const;

const NEXT_LABEL: Record<Theme, string> = {
  light:  "Switch to dark theme",
  dark:   "Switch to system theme",
  system: "Switch to light theme",
};

/**
 * Icon-only theme cycler. One click cycles light → dark → system →
 * light. The visible icon is the *current* state; the cute spin/scale
 * animation comes from stacking all three icons in the same square and
 * fading the inactive ones out via Tailwind transitions on a data
 * attribute.
 */
export function ThemePicker() {
  const [theme, setTheme] = useState<Theme>(getStoredTheme());

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  const next = () => {
    const i = ORDER.indexOf(theme);
    setTheme(ORDER[(i + 1) % ORDER.length]);
  };

  return (
    <button
      type="button"
      onClick={next}
      aria-label={NEXT_LABEL[theme]}
      title={NEXT_LABEL[theme]}
      className="relative inline-flex h-8 w-8 items-center justify-center rounded-md border border-border bg-surface-muted text-fg-muted transition-colors hover:text-fg"
    >
      <Icon active={theme === "light"}>
        <Sun />
      </Icon>
      <Icon active={theme === "dark"}>
        <Moon />
      </Icon>
      <Icon active={theme === "system"}>
        <Monitor />
      </Icon>
    </button>
  );
}

/**
 * Stacks an icon at full size in the parent's center. When inactive,
 * the icon spins 90deg and scales to 0 on its way out — and back in
 * the other direction on the way in. 250ms feels lively without being
 * fussy.
 */
function Icon({ active, children }: { active: boolean; children: React.ReactNode }) {
  return (
    <span
      data-state={active ? "active" : "inactive"}
      className="absolute inset-0 flex items-center justify-center transition-all duration-300 ease-out
                 data-[state=active]:rotate-0  data-[state=active]:scale-100  data-[state=active]:opacity-100
                 data-[state=inactive]:-rotate-90 data-[state=inactive]:scale-0  data-[state=inactive]:opacity-0"
    >
      {children}
    </span>
  );
}

function Sun() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function Moon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

function Monitor() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="2" y="4" width="20" height="14" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </svg>
  );
}
