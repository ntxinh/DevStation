"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { HealthBadge } from "@/features/health/HealthBadge";
import type { HealthOverview } from "@/features/health/types";
import { useHealthUpdates } from "@/features/health/useHealthUpdates";
import { apiFetch } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";

export default function HealthPage() {
  const qc = useQueryClient();
  useHealthUpdates();

  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["health", "overview"],
    queryFn: () => apiFetch<HealthOverview>("/api/health/overview"),
    refetchInterval: 30_000, // fallback when SignalR is unreachable
  });

  const run = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/health/${id}/check`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["health"] }),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Health</h1>
        <button
          onClick={() => refetch()}
          className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
        >
          {isRefetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.checks.length === 0 ? <EmptyState title="No health checks configured" /> : null}
      {data && data.checks.length > 0 ? (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
          {data.checks.map(({ check, last, uptime24h }) => (
            <div key={check.id} className="rounded-lg border border-border bg-panel p-3 text-xs">
              <div className="flex items-center justify-between gap-2">
                <span className="font-medium">{check.name}</span>
                <HealthBadge status={last?.status} />
              </div>
              <div className="mt-1 truncate font-mono text-muted">
                {check.kind} · {check.target}
              </div>
              <div className="mt-2 flex items-center gap-3 text-muted">
                <span>{last?.latencyMs != null ? `${last.latencyMs}ms` : "—"}</span>
                <span>uptime {uptime24h}%</span>
                <span>{last ? formatRelativeTime(last.checkedAt) : "never run"}</span>
                <span className="flex-1" />
                <button
                  onClick={() => run.mutate(check.id)}
                  disabled={run.isPending}
                  className="rounded border border-border px-2 py-0.5 hover:text-fg disabled:opacity-40"
                >
                  Run now
                </button>
              </div>
              {last?.error ? (
                <div className="mt-1 truncate text-err" title={last.error}>{last.error}</div>
              ) : null}
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}
