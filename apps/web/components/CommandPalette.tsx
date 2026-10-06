"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import { ConfirmDialog } from "@/components/ConfirmDialog";

interface DevStationCommand {
  id: string;
  title: string;
  verb: "navigation" | "query" | "action";
  target: string;
  requiredTier: string;
  needsConfirm: boolean;
}

interface SearchHit {
  id: string;
  name: string;
  detail: string | null;
  status: string | null;
}

interface SearchGroups {
  projects: SearchHit[];
  environments: SearchHit[];
  services: SearchHit[];
  jobs: SearchHit[];
  databases: SearchHit[];
  deployments: SearchHit[];
  integrations: SearchHit[];
  containers: SearchHit[];
}

const GROUP_ROUTES: Record<keyof SearchGroups, string> = {
  projects: "/projects",
  environments: "/environments",
  services: "/services",
  jobs: "/jobs",
  databases: "/databases",
  deployments: "/deployments",
  integrations: "/integrations",
  containers: "/containers",
};

// groups that have a /:id detail page
const HAS_DETAIL: Record<keyof SearchGroups, true | undefined> = {
  projects: true,
  environments: true,
  services: undefined,
  jobs: true,
  databases: undefined,
  deployments: true,
  integrations: undefined,
  containers: true,
};

type Item =
  | { kind: "command"; label: string; detail: string | null; command: DevStationCommand }
  | { kind: "hit"; label: string; detail: string | null; href: string };

/**
 * ⌘K palette: commands from /api/commands (title-matched client-side) merged
 * with /api/search hits when the query is ≥2 chars. ↑↓/enter navigate, esc or
 * backdrop closes; needsConfirm actions go through ConfirmDialog first.
 */
export function CommandPalette({ onClose }: { onClose: () => void }) {
  const router = useRouter();
  const [q, setQ] = useState("");
  const [index, setIndex] = useState(0);
  const [pending, setPending] = useState<DevStationCommand | null>(null);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => inputRef.current?.focus(), []);

  const { data: commands } = useQuery({
    queryKey: ["commands"],
    queryFn: () => apiFetch<DevStationCommand[]>("/api/commands"),
    staleTime: 15_000,
  });
  const { data: hits } = useQuery({
    queryKey: ["search", q],
    queryFn: () => apiFetch<SearchGroups>(`/api/search?q=${encodeURIComponent(q)}`),
    enabled: q.trim().length >= 2,
  });

  const query = q.trim().toLowerCase();
  const items = useMemo<Item[]>(() => {
    const cmds = (commands ?? [])
      .filter((c) => query === "" || c.title.toLowerCase().includes(query))
      .map((c): Item => ({ kind: "command", label: c.title, detail: c.requiredTier, command: c }));
    const search: Item[] = query.length >= 2
      ? (Object.keys(GROUP_ROUTES) as (keyof SearchGroups)[]).flatMap((g) =>
          (hits?.[g] ?? []).map((h): Item => ({
            kind: "hit",
            label: h.name,
            detail: `${g}${h.detail ? ` · ${h.detail}` : ""}`,
            href: HAS_DETAIL[g] ? `${GROUP_ROUTES[g]}/${h.id}` : GROUP_ROUTES[g],
          })))
      : [];
    return [...cmds, ...search];
  }, [commands, hits, query]);

  const firstSearch = items.findIndex((i) => i.kind === "hit");
  const clamp = (i: number) => Math.max(0, Math.min(i, items.length - 1));

  async function run(cmd: DevStationCommand, confirmed: boolean) {
    if (cmd.verb === "navigation") {
      onClose();
      router.push(cmd.target);
      return;
    }
    if (cmd.needsConfirm && !confirmed) {
      setPending(cmd);
      return;
    }
    setBusy(true);
    try {
      const res = await apiFetch<{ route: string | null; result: unknown }>(
        `/api/commands/${encodeURIComponent(cmd.id)}/execute`,
        { method: "POST", body: JSON.stringify({ confirm: confirmed }) },
      );
      onClose();
      if (res.route) router.push(res.route);
    } finally {
      setBusy(false);
    }
  }

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") { e.preventDefault(); setIndex((i) => clamp(i + 1)); }
    else if (e.key === "ArrowUp") { e.preventDefault(); setIndex((i) => clamp(i - 1)); }
    else if (e.key === "Enter") {
      e.preventDefault();
      const item = items[clamp(index)];
      if (!item || busy) return;
      if (item.kind === "hit") { onClose(); router.push(item.href); }
      else void run(item.command, false);
    }
  };

  const rowFor = (item: Item, i: number) => (
    <button
      key={item.kind === "command" ? item.command.id : item.href + item.label}
      className={`flex w-full items-center gap-2 rounded px-2 py-1.5 text-left ${i === clamp(index) ? "bg-bg text-accent" : "text-fg"}`}
      onMouseEnter={() => setIndex(i)}
      onClick={() => {
        if (item.kind === "hit") { onClose(); router.push(item.href); }
        else void run(item.command, false);
      }}
    >
      <span className="flex-1">{item.label}</span>
      {item.detail ? <span className="text-muted">{item.detail}</span> : null}
    </button>
  );

  return (
    <>
      <div className="w-full max-w-md rounded-lg border border-border bg-panel text-xs" onKeyDown={onKey}>
        <input
          ref={inputRef}
          value={q}
          onChange={(e) => { setQ(e.target.value); setIndex(0); }}
          placeholder="Type a command or search…"
          aria-label="Command palette"
          className="w-full border-b border-border bg-transparent px-3 py-2.5 text-sm outline-none"
        />
        <div className="max-h-80 overflow-auto p-1">
          {items.length === 0 ? (
            <div className="px-2 py-3 text-muted">No matching commands.</div>
          ) : (
            <>
              <div className="px-2 pt-1 pb-1 text-[10px] uppercase tracking-wide text-muted">Commands</div>
              {(firstSearch === -1 ? items : items.slice(0, firstSearch)).map((item, i) => rowFor(item, i))}
              {firstSearch !== -1 ? (
                <>
                  <div className="px-2 pt-2 pb-1 text-[10px] uppercase tracking-wide text-muted">Search results</div>
                  {items.slice(firstSearch).map((item, i) => rowFor(item, firstSearch + i))}
                </>
              ) : null}
            </>
          )}
        </div>
        <div className="flex gap-3 border-t border-border px-3 py-1.5 text-[10px] text-muted">
          <span><kbd>↑↓</kbd> navigate</span>
          <span><kbd>↵</kbd> select</span>
          <span><kbd>esc</kbd> close</span>
          {busy ? <span className="ml-auto">Running…</span> : null}
        </div>
      </div>
      <ConfirmDialog
        open={pending !== null}
        title={pending?.title ?? ""}
        description="This action changes runtime state. Confirm to execute."
        confirmLabel="Execute"
        onCancel={() => setPending(null)}
        onConfirm={() => {
          const cmd = pending;
          setPending(null);
          if (cmd) void run(cmd, true);
        }}
      />
    </>
  );
}
