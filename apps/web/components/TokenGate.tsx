"use client";

import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { LuKeyRound } from "react-icons/lu";
import { TOKEN_KEY } from "@/lib/api";

export function TokenGate({ children }: { children: ReactNode }) {
  // null until mounted so SSR markup matches the pre-auth client render
  const [hasToken, setHasToken] = useState<boolean | null>(null);
  const [value, setValue] = useState("");

  useEffect(() => setHasToken(!!localStorage.getItem(TOKEN_KEY)), []);

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!value.trim()) return;
    localStorage.setItem(TOKEN_KEY, value.trim());
    location.reload();
  }

  if (hasToken) return <>{children}</>;

  return (
    <div className="flex min-h-screen items-center justify-center">
      <form
        onSubmit={onSubmit}
        className="flex w-full max-w-xs flex-col gap-3 rounded-lg border border-border bg-panel p-6"
      >
        <div className="flex items-center gap-2">
          <LuKeyRound className="h-4 w-4 text-accent" aria-hidden />
          <h1 className="text-sm font-semibold">DevStation token</h1>
        </div>
        <p className="text-xs text-muted">Enter a dev token to connect to the API.</p>
        <input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="dev token"
          autoFocus
          className="w-full rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        />
        <button type="submit" className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-black">
          Connect
        </button>
      </form>
    </div>
  );
}
