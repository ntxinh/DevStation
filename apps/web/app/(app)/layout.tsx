"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Nav } from "@/components/Nav";
import { TokenGate } from "@/components/TokenGate";
import { Topbar } from "@/components/Topbar";
import { apiFetch } from "@/lib/api";
import { useUiStore } from "@/stores/ui";

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

function SearchPalette({ onClose }: { onClose: () => void }) {
  const [q, setQ] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => inputRef.current?.focus(), []);

  const { data } = useQuery({
    queryKey: ["search", q],
    queryFn: () => apiFetch<SearchGroups>(`/api/search?q=${encodeURIComponent(q)}`),
    enabled: q.trim().length >= 2,
  });

  const groups = (Object.keys(GROUP_ROUTES) as (keyof SearchGroups)[]).filter(
    (g) => (data?.[g]?.length ?? 0) > 0,
  );

  return (
    <div className="w-full max-w-md rounded-lg border border-border bg-panel text-xs">
      <input
        ref={inputRef}
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Search projects, environments, services…"
        aria-label="Global search"
        className="w-full border-b border-border bg-transparent px-3 py-2.5 text-sm outline-none"
      />
      <div className="max-h-80 overflow-auto p-1">
        {q.trim().length < 2 ? (
          <div className="px-2 py-3 text-muted">
            Type at least 2 characters to search.
          </div>
        ) : groups.length === 0 ? (
          <div className="px-2 py-3 text-muted">No results.</div>
        ) : (
          groups.map((g) => (
            <div key={g}>
              <div className="px-2 pt-2 pb-1 text-[10px] uppercase tracking-wide text-muted">
                {g}
              </div>
              {data![g].map((hit) => (
                <Link
                  key={hit.id}
                  href={HAS_DETAIL[g] ? `${GROUP_ROUTES[g]}/${hit.id}` : GROUP_ROUTES[g]}
                  onClick={onClose}
                  className="block rounded px-2 py-1.5 hover:bg-bg"
                >
                  <span className="text-fg">{hit.name}</span>
                  {hit.detail ? (
                    <span className="ml-2 text-muted">{hit.detail}</span>
                  ) : null}
                  {hit.status ? (
                    <span className="ml-2 text-muted">({hit.status})</span>
                  ) : null}
                </Link>
              ))}
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default function AppLayout({ children }: { children: ReactNode }) {
  const { paletteOpen, setPaletteOpen } = useUiStore();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen(true);
      }
      if (e.key === "Escape") setPaletteOpen(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [setPaletteOpen]);

  return (
    <TokenGate>
      <div className="flex h-screen">
        <Nav />
        <div className="flex min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="flex-1 overflow-auto p-4">{children}</main>
        </div>
      </div>
      {paletteOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center bg-black/60 pt-32"
          onClick={() => setPaletteOpen(false)}
        >
          <div onClick={(e) => e.stopPropagation()}>
            <SearchPalette onClose={() => setPaletteOpen(false)} />
          </div>
        </div>
      ) : null}
    </TokenGate>
  );
}
