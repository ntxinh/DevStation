"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import { DeployDialog } from "@/features/deployments/DeployDialog";
import type { Deployment } from "@/features/deployments/types";
import { useDeploymentUpdates } from "@/features/deployments/useDeploymentUpdates";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";


export default function DeploymentsPage() {
  const qc = useQueryClient();
  const router = useRouter();
  useDeploymentUpdates();
  const [deployOpen, setDeployOpen] = useState(false);
  const [cancelTarget, setCancelTarget] = useState<Deployment | null>(null);

  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["deployments"],
    queryFn: () => apiFetch<Deployment[]>("/api/deployments"),
    refetchInterval: 30_000,
  });

  const cancel = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/deployments/${id}/cancel`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["deployments"] }),
    onSettled: () => setCancelTarget(null),
  });


  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Deployments</h1>
        <div className="flex gap-2">
          <button
            onClick={() => refetch()}
            className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
          >
            {isRefetching ? "Refreshing…" : "Refresh"}
          </button>
          <button
            onClick={() => setDeployOpen(true)}
            className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-white"
          >
            Deploy
          </button>
        </div>
      </div>

      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {cancel.error ? <ErrorState error={cancel.error} /> : null}
      {data && data.length === 0 ? (
        <EmptyState title="No deployments">
          No deployments yet — click Deploy to start one.
        </EmptyState>
      ) : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Project</th>
              <th className="py-1.5 pr-3 font-medium">Environment</th>
              <th className="py-1.5 pr-3 font-medium">Commit</th>
              <th className="py-1.5 pr-3 font-medium">Status</th>
              <th className="py-1.5 pr-3 font-medium">Duration</th>
              <th className="py-1.5 pr-3 font-medium">Started</th>
              <th className="py-1.5 font-medium" />
            </tr>
          </thead>
          <tbody>
            {data.map((d) => (
              <tr key={d.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3">
                  <Link href={`/deployments/${d.id}`} className="text-fg hover:text-accent">
                    {d.projectName ?? "—"}
                  </Link>
                </td>
                <td className="py-1.5 pr-3 text-muted">{d.environmentName ?? "—"}</td>
                <td className="py-1.5 pr-3 font-mono text-muted">
                  {d.commit ? d.commit.slice(0, 10) : "—"}
                </td>
                <td className="py-1.5 pr-3">
                  <StatusBadge status={d.status as BadgeStatus} />
                </td>
                <td className="py-1.5 pr-3 font-mono text-muted">{d.durationMs != null ? `${Math.round(d.durationMs / 100) / 10}s` : "—"}</td>
                <td className="py-1.5 pr-3 text-muted">{formatDateTime(d.startedAt ?? d.createdAt)}</td>
                <td className="py-1.5 text-right">
                  {d.status === "queued" || d.status === "running" ? (
                    <button
                      onClick={() => setCancelTarget(d)}
                      disabled={cancel.isPending}
                      className="rounded border border-border px-2 py-0.5 text-muted hover:text-fg disabled:opacity-40"
                    >
                      Cancel
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      <DeployDialog
        open={deployOpen}
        onClose={() => setDeployOpen(false)}
        onDeployed={(d) => {
          qc.invalidateQueries({ queryKey: ["deployments"] });
          router.push(`/deployments/${d.id}`);
        }}
      />
      <ConfirmDialog
        open={cancelTarget !== null}
        title="Cancel deployment?"
        description={`Stop the ${cancelTarget?.environmentName ?? ""} deployment.`}
        confirmLabel="Cancel deployment"
        onConfirm={() => cancelTarget && cancel.mutate(cancelTarget.id)}
        onCancel={() => setCancelTarget(null)}
      />
    </div>
  );
}
