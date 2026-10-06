"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import type { JobInfo } from "@/features/jobs/types";
import { useJobUpdates } from "@/features/jobs/useJobUpdates";
import { apiFetch } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";

export default function JobsPage() {
  const qc = useQueryClient();
  useJobUpdates();
  const [runTarget, setRunTarget] = useState<JobInfo | null>(null);

  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["jobs"],
    queryFn: () => apiFetch<JobInfo[]>("/api/jobs"),
    refetchInterval: 30_000,
  });

  const run = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/jobs/${id}/trigger?confirm=true`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["jobs"] }),
    onSettled: () => setRunTarget(null),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Jobs</h1>
        <button
          onClick={() => refetch()}
          className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
        >
          {isRefetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {run.error ? <ErrorState error={run.error} /> : null}
      {data && data.length === 0 ? (
        <EmptyState title="No jobs">
          No jobs registered — add entries under the Jobs section of appsettings.
        </EmptyState>
      ) : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Name</th>
              <th className="py-1.5 pr-3 font-medium">Schedule</th>
              <th className="py-1.5 pr-3 font-medium">Timezone</th>
              <th className="py-1.5 pr-3 font-medium">Next run</th>
              <th className="py-1.5 pr-3 font-medium">Last run</th>
              <th className="py-1.5 pr-3 font-medium">Status</th>
              <th className="py-1.5 pr-3 font-medium">Runs</th>
              <th className="py-1.5 font-medium" />
            </tr>
          </thead>
          <tbody>
            {data.map((j) => (
              <tr key={j.id || j.name} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3">
                  <Link href={`/jobs/${j.id}`} className="text-fg hover:text-accent">
                    {j.name}
                  </Link>
                </td>
                <td className="py-1.5 pr-3 font-mono text-muted">{j.schedule ?? "—"}</td>
                <td className="py-1.5 pr-3 text-muted">{j.timezone ?? "—"}</td>
                <td className="py-1.5 pr-3 text-muted">
                  {j.nextRun ? formatRelativeTime(j.nextRun) : "—"}
                </td>
                <td className="py-1.5 pr-3 text-muted">
                  {j.lastRun ? formatRelativeTime(j.lastRun) : "never"}
                </td>
                <td className="py-1.5 pr-3">
                  <StatusBadge status={j.status as BadgeStatus} />
                </td>
                <td className="py-1.5 pr-3 font-mono text-muted">
                  <span className="text-ok">{j.successCount}</span>
                  {" / "}
                  <span className={j.failureCount > 0 ? "text-err" : ""}>{j.failureCount}</span>
                </td>
                <td className="py-1.5 text-right">
                  <button
                    onClick={() => setRunTarget(j)}
                    disabled={run.isPending || j.status === "paused"}
                    className="rounded border border-border px-2 py-0.5 text-muted hover:text-fg disabled:opacity-40"
                  >
                    Run now
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      <ConfirmDialog
        open={runTarget !== null}
        title="Run job now?"
        description={`Run "${runTarget?.name}" immediately, outside its schedule.`}
        confirmLabel="Run"
        onConfirm={() => runTarget && run.mutate(runTarget.id)}
        onCancel={() => setRunTarget(null)}
      />
    </div>
  );
}
