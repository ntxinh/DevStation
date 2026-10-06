"use client";

import { useQuery } from "@tanstack/react-query";
import { LuPanelLeft, LuSearch } from "react-icons/lu";
import { apiFetch } from "@/lib/api";
import { NotificationsBell } from "@/components/NotificationsBell";
import { useUiStore } from "@/stores/ui";

interface Environment {
  id: string;
  name: string;
}

export function Topbar() {
  const { toggleSidebar, setPaletteOpen, selectedEnvironmentId, selectEnvironment } = useUiStore();
  const { data: envs } = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<Environment[]>("/api/environments"),
    retry: false,
  });

  return (
    <header className="flex h-12 shrink-0 items-center gap-3 border-b border-border bg-panel px-3">
      <button onClick={toggleSidebar} aria-label="Toggle sidebar" className="text-muted hover:text-fg">
        <LuPanelLeft className="h-4 w-4" aria-hidden />
      </button>
      <button
        onClick={() => setPaletteOpen(true)}
        className="flex w-64 items-center gap-2 rounded border border-border bg-bg px-2 py-1.5 text-xs text-muted hover:border-accent"
      >
        <LuSearch className="h-3.5 w-3.5" aria-hidden />
        <span className="flex-1 text-left">Search or jump to…</span>
        <kbd className="rounded border border-border px-1 text-[10px]">⌘K</kbd>
      </button>
      <div className="flex-1" />
      <NotificationsBell />
      <select
        value={selectedEnvironmentId ?? ""}
        onChange={(e) => selectEnvironment(e.target.value || null)}
        aria-label="Environment"
        className="rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
      >
        <option value="">All environments</option>
        {(envs ?? []).map((e) => (
          <option key={e.id} value={e.id}>
            {e.name}
          </option>
        ))}
      </select>
    </header>
  );
}
