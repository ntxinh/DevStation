"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge } from "@/components/StatusBadge";
import type { ContainerInfo } from "@/features/containers/types";
import { apiFetch } from "@/lib/api";

export default function ContainersPage() {
  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["containers"],
    queryFn: () => apiFetch<ContainerInfo[]>("/api/containers"),
    refetchInterval: 30_000,
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Containers</h1>
        <button
          onClick={() => refetch()}
          className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
        >
          {isRefetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.length === 0 ? (
        <EmptyState title="No containers" >
          No containers found on this host.
        </EmptyState>
      ) : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Name</th>
              <th className="py-1.5 pr-3 font-medium">Image</th>
              <th className="py-1.5 pr-3 font-medium">State</th>
              <th className="py-1.5 pr-3 font-medium">Status</th>
              <th className="py-1.5 font-medium">Ports</th>
            </tr>
          </thead>
          <tbody>
            {data.map((c) => (
              <tr key={c.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3">
                  <Link href={`/containers/${c.id}`} className="text-fg hover:text-accent">
                    {c.name || c.id.slice(0, 12)}
                  </Link>
                </td>
                <td className="max-w-64 truncate py-1.5 pr-3 font-mono text-muted">
                  {c.image}
                </td>
                <td className="py-1.5 pr-3">
                  <StatusBadge status={c.state} />
                </td>
                <td className="py-1.5 pr-3 text-muted">{c.status}</td>
                <td className="py-1.5 font-mono text-muted">
                  {c.ports.length > 0 ? c.ports.join(", ") : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
