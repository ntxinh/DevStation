"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import type { Deployment } from "@/features/deployments/types";
import { useDeploymentUpdates } from "@/features/deployments/useDeploymentUpdates";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import { AiSuggestionPanel } from "@/components/AiSuggestionPanel";
import { explainError } from "@/features/ai/api";

export default function DeploymentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  useDeploymentUpdates();
  const [confirmCancel, setConfirmCancel] = useState(false);
  const explain = useMutation({ mutationFn: explainError });

  const { data, error, isLoading } = useQuery({
    queryKey: ["deployments", id],
    queryFn: () => apiFetch<Deployment>(`/api/deployments/${id}`),
    refetchInterval: (q) =>
      q.state.data && (q.state.data.status === "queued" || q.state.data.status === "running")
        ? 2000
        : false,
  });

  const cancel = useMutation({
    mutationFn: () => apiFetch(`/api/deployments/${id}/cancel`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["deployments"] }),
    onSettled: () => setConfirmCancel(false),
  });


  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">
          <Link href="/deployments" className="text-muted hover:text-fg">Deployments</Link>
          <span className="mx-2 text-muted">/</span>
          {data?.environmentName ?? id.slice(0, 12)}
          {data?.commit ? (
            <span className="ml-2 font-mono text-xs text-muted">@ {data.commit.slice(0, 10)}</span>
          ) : null}
        </h1>
        {data && (data.status === "queued" || data.status === "running") ? (
          <button
            onClick={() => setConfirmCancel(true)}
            disabled={cancel.isPending}
            className="rounded border border-err px-3 py-1.5 text-xs text-err hover:bg-err/10 disabled:opacity-40"
          >
            Cancel
          </button>
        ) : null}
        {data?.status === "failed" ? (
          <button
            onClick={() => explain.mutate({
              logId: id,
              logText: data.logs.join("\n") || undefined,
            })}
            disabled={explain.isPending}
            className="rounded border border-accent px-3 py-1.5 text-xs text-accent hover:bg-accent/10 disabled:opacity-40"
          >
            Explain failure
          </button>
        ) : null}
      </div>

      {explain.isPending || explain.data || explain.error ? (
        <AiSuggestionPanel
          title="Explain failure"
          loading={explain.isPending}
          error={explain.error ? String(explain.error) : null}
          result={explain.data ?? null}
        />
      ) : null}

      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {cancel.error ? <ErrorState error={cancel.error} /> : null}

      {data ? (
        <div className="rounded-lg border border-border bg-panel p-3 text-xs">
          <dl className="grid grid-cols-[7rem_1fr] gap-y-1">
            <dt className="text-muted">Status</dt>
            <dd><StatusBadge status={data.status as BadgeStatus} /></dd>
            <dt className="text-muted">Project</dt>
            <dd>{data.projectName ?? "—"}</dd>
            <dt className="text-muted">Environment</dt>
            <dd>{data.environmentName ?? "—"}</dd>
            <dt className="text-muted">Commit</dt>
            <dd className="font-mono">{data.commit ?? "—"}</dd>
            {data.version ? (
              <>
                <dt className="text-muted">Version</dt>
                <dd className="font-mono">{data.version}</dd>
              </>
            ) : null}
            <dt className="text-muted">Started</dt>
            <dd>{formatDateTime(data.startedAt)}</dd>
            <dt className="text-muted">Finished</dt>
            <dd>{formatDateTime(data.finishedAt)}</dd>
            <dt className="text-muted">Duration</dt>
            <dd className="font-mono">
              {data.durationMs != null ? `${Math.round(data.durationMs / 100) / 10}s` : "—"}
            </dd>
          </dl>
        </div>
      ) : null}

      <div className="rounded-lg border border-border bg-panel p-3 text-xs">
        <h2 className="mb-2 font-medium text-muted">Logs</h2>
        {data && data.logs.length === 0 ? (
          <div className="text-muted">No log lines yet.</div>
        ) : null}
        {data && data.logs.length > 0 ? (
          <pre className="max-h-96 overflow-auto font-mono leading-relaxed">
            {data.logs.map((l, i) => (
              <div key={i}>{l}</div>
            ))}
          </pre>
        ) : null}
      </div>

      <ConfirmDialog
        open={confirmCancel}
        title="Cancel deployment?"
        description="Stop this deployment in place."
        confirmLabel="Cancel deployment"
        onConfirm={() => cancel.mutate()}
        onCancel={() => setConfirmCancel(false)}
      />
    </div>
  );
}
