"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import type { JobDetail } from "@/features/jobs/types";
import { useJobUpdates } from "@/features/jobs/useJobUpdates";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";

type Action = "trigger" | "pause" | "resume";

const ACTION_LABEL: Record<Action, { title: string; desc: string; btn: string }> = {
  trigger: { title: "Run job now?", desc: "Queue an immediate run outside the schedule.", btn: "Run" },
  pause: { title: "Pause job?", desc: "Scheduled triggers stop firing until resumed.", btn: "Pause" },
  resume: { title: "Resume job?", desc: "Scheduled triggers resume firing.", btn: "Resume" },
};

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  useJobUpdates();
  const [action, setAction] = useState<Action | null>(null);

  const { data, error, isLoading } = useQuery({
    queryKey: ["jobs", id],
    queryFn: () => apiFetch<JobDetail>(`/api/jobs/${id}`),
  });

  const act = useMutation({
    mutationFn: (a: Action) =>
      apiFetch(`/api/jobs/${id}/${a}${a === "trigger" ? "?confirm=true" : ""}`,
        { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["jobs"] }),
    onSettled: () => setAction(null),
  });

  const j = data?.job;
  const execs = data?.executions ?? [];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">
          <Link href="/jobs" className="text-muted hover:text-fg">Jobs</Link>
          <span className="mx-2 text-muted">/</span>
          {j?.name ?? id.slice(0, 12)}
        </h1>
        <div className="flex gap-2">
          <button
            onClick={() => setAction("trigger")}
            disabled={act.isPending || j?.status === "paused"}
            className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg disabled:opacity-40"
          >
            Run now
          </button>
          {j?.status === "paused" ? (
            <button
              onClick={() => setAction("resume")}
              disabled={act.isPending}
              className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg disabled:opacity-40"
            >
              Resume
            </button>
          ) : (
            <button
              onClick={() => setAction("pause")}
              disabled={act.isPending}
              className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg disabled:opacity-40"
            >
              Pause
            </button>
          )}
        </div>
      </div>

      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {act.error ? <ErrorState error={act.error} /> : null}

      {j ? (
        <div className="rounded-lg border border-border bg-panel p-3 text-xs">
          <dl className="grid grid-cols-[6rem_1fr] gap-y-1">
            <dt className="text-muted">Status</dt>
            <dd><StatusBadge status={j.status as BadgeStatus} /></dd>
            {j.description ? (
              <>
                <dt className="text-muted">Description</dt>
                <dd>{j.description}</dd>
              </>
            ) : null}
            <dt className="text-muted">Schedule</dt>
            <dd className="font-mono">{j.schedule ?? "—"}{j.timezone ? ` (${j.timezone})` : ""}</dd>
            <dt className="text-muted">Last run</dt>
            <dd>{formatDateTime(j.lastRun)}{j.durationMs != null ? ` · ${j.durationMs}ms` : ""}</dd>
            <dt className="text-muted">Next run</dt>
            <dd>{formatDateTime(j.nextRun)}</dd>
            <dt className="text-muted">Runs</dt>
            <dd>
              <span className="text-ok">{j.successCount} ok</span> ·{" "}
              <span className={j.failureCount > 0 ? "text-err" : ""}>{j.failureCount} failed</span>
            </dd>
          </dl>
        </div>
      ) : null}

      <div className="rounded-lg border border-border bg-panel p-3 text-xs">
        <h2 className="mb-2 font-medium text-muted">Executions</h2>
        {execs.length === 0 ? <div className="text-muted">No executions yet.</div> : null}
        {execs.length > 0 ? (
          <table className="w-full border-collapse">
            <thead>
              <tr className="border-b border-border text-left text-muted">
                <th className="py-1.5 pr-3 font-medium">Started</th>
                <th className="py-1.5 pr-3 font-medium">Finished</th>
                <th className="py-1.5 pr-3 font-medium">Duration</th>
                <th className="py-1.5 pr-3 font-medium">Status</th>
                <th className="py-1.5 font-medium">Error</th>
              </tr>
            </thead>
            <tbody>
              {execs.map((e) => (
                <tr key={e.id} className="border-b border-border/50">
                  <td className="py-1.5 pr-3">{formatDateTime(e.startedAt)}</td>
                  <td className="py-1.5 pr-3">{formatDateTime(e.endedAt)}</td>
                  <td className="py-1.5 pr-3 font-mono">
                    {e.durationMs != null ? `${e.durationMs}ms` : "—"}
                  </td>
                  <td className="py-1.5 pr-3">
                    <StatusBadge status={e.status as BadgeStatus} />
                  </td>
                  <td className="max-w-64 truncate py-1.5 font-mono text-err" title={e.error ?? ""}>
                    {e.error ?? "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>

      <ConfirmDialog
        open={action !== null}
        title={action ? ACTION_LABEL[action].title : ""}
        description={action ? ACTION_LABEL[action].desc : ""}
        confirmLabel={action ? ACTION_LABEL[action].btn : "Confirm"}
        onConfirm={() => action && act.mutate(action)}
        onCancel={() => setAction(null)}
      />
    </div>
  );
}
