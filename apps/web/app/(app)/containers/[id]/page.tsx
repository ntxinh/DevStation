"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { ApiError, apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge } from "@/components/StatusBadge";
import type { ContainerInspect, ContainerStats } from "@/features/containers/types";

type Action = "start" | "stop" | "restart";

const TAILS = [50, 200, 500, 1000, 5000];

export default function ContainerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [action, setAction] = useState<Action | null>(null);
  const [tail, setTail] = useState(200);

  const detail = useQuery({
    queryKey: ["containers", id],
    queryFn: () => apiFetch<ContainerInspect>(`/api/containers/${id}`),
  });
  const logs = useQuery({
    queryKey: ["containers", id, "logs", tail],
    queryFn: () => apiFetch<string[]>(`/api/containers/${id}/logs?tail=${tail}`),
    enabled: detail.isSuccess,
  });
  const stats = useQuery({
    queryKey: ["containers", id, "stats"],
    queryFn: () => apiFetch<ContainerStats>(`/api/containers/${id}/stats`),
    enabled: detail.isSuccess,
    retry: false,
  });
  // stats unsupported/erroring runtimes → section hidden, not a page error
  const showStats = stats.data ?? null;

  const act = useMutation({
    mutationFn: (a: Action) =>
      apiFetch(`/api/containers/${id}/${a}?confirm=true`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["containers"] }),
    onSettled: () => setAction(null),
  });

  const c = detail.data;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">
          <Link href="/containers" className="text-muted hover:text-fg">Containers</Link>
          <span className="mx-2 text-muted">/</span>
          {c?.name ?? id.slice(0, 12)}
        </h1>
        <div className="flex gap-2">
          {(["start", "stop", "restart"] as Action[]).map((a) => (
            <button
              key={a}
              onClick={() => setAction(a)}
              disabled={act.isPending}
              className="rounded border border-border px-3 py-1.5 text-xs capitalize text-muted hover:text-fg disabled:opacity-40"
            >
              {a}
            </button>
          ))}
        </div>
      </div>

      {detail.isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {detail.error ? <ErrorState error={detail.error} /> : null}
      {act.error ? <ErrorState error={act.error} /> : null}

      {c ? (
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="mb-2 font-medium text-muted">Info</h2>
            <dl className="grid grid-cols-[6rem_1fr] gap-y-1">
              <dt className="text-muted">Id</dt>
              <dd className="truncate font-mono">{c.id}</dd>
              <dt className="text-muted">Image</dt>
              <dd className="truncate font-mono">{c.image}</dd>
              <dt className="text-muted">State</dt>
              <dd><StatusBadge status={c.state} /></dd>
              <dt className="text-muted">Exit code</dt>
              <dd>{c.exitCode ?? "—"}</dd>
              <dt className="text-muted">Created</dt>
              <dd>{formatDateTime(c.createdAt)}</dd>
              <dt className="text-muted">Started</dt>
              <dd>{formatDateTime(c.startedAt)}</dd>
            </dl>
          </div>
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="mb-2 font-medium text-muted">
              Env <span className="text-muted/60">(secrets filtered server-side)</span>
            </h2>
            {c.env.length === 0 ? <div className="text-muted">—</div> : (
              <ul className="max-h-40 space-y-0.5 overflow-auto font-mono text-muted">
                {c.env.map((e) => <li key={e} className="truncate" title={e}>{e}</li>)}
              </ul>
            )}
          </div>
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="mb-2 font-medium text-muted">Mounts &amp; Networks</h2>
            {c.mounts.length === 0 && c.networks.length === 0
              ? <div className="text-muted">—</div> : null}
            <ul className="space-y-0.5 font-mono text-muted">
              {c.mounts.map((m) => (
                <li key={`${m.source}${m.destination}`} className="truncate" title={`${m.source} → ${m.destination}`}>
                  {m.type} {m.source} → {m.destination}{m.readWrite ? "" : " (ro)"}
                </li>
              ))}
              {c.networks.map((n) => (
                <li key={n.name}>{n.name}{n.ipAddress ? ` ${n.ipAddress}` : ""}</li>
              ))}
            </ul>
          </div>
        </div>
      ) : null}

      {showStats ? (
        <div className="rounded-lg border border-border bg-panel p-3 text-xs">
          <h2 className="mb-2 font-medium text-muted">Stats</h2>
          <div className="flex flex-wrap gap-4 font-mono text-muted">
            <span>cpu {showStats.cpuPercent ?? "—"}</span>
            <span>mem {showStats.memUsage ?? "—"}{showStats.memPercent ? ` (${showStats.memPercent})` : ""}</span>
            <span>net {showStats.netIo ?? "—"}</span>
            {showStats.blockIo ? <span>io {showStats.blockIo}</span> : null}
            <span>pids {showStats.pids ?? "—"}</span>
          </div>
        </div>
      ) : null}

      <div className="rounded-lg border border-border bg-panel p-3 text-xs">
        <div className="mb-2 flex items-center gap-3">
          <h2 className="font-medium text-muted">Logs</h2>
          <select
            value={tail}
            onChange={(e) => setTail(Number(e.target.value))}
            aria-label="Log tail"
            className="rounded border border-border bg-bg px-2 py-0.5 text-xs"
          >
            {TAILS.map((n) => <option key={n} value={n}>last {n}</option>)}
          </select>
          <button
            onClick={() => logs.refetch()}
            disabled={logs.isFetching}
            className="rounded border border-border px-2 py-0.5 text-muted hover:text-fg disabled:opacity-40"
          >
            {logs.isFetching ? "Refreshing…" : "Refresh"}
          </button>
        </div>
        {logs.error instanceof ApiError ? (
          <div className="text-err">{logs.error.message}</div>
        ) : null}
        <pre className="max-h-96 overflow-auto rounded bg-bg p-2 font-mono text-[11px] leading-relaxed text-muted">
          {logs.data && logs.data.length > 0 ? logs.data.join("\n") : "No log output."}
        </pre>
      </div>

      <ConfirmDialog
        open={action !== null}
        title={`${action ?? ""} ${c?.name ?? "container"}`}
        description={`Runs ${action} on container ${c?.name ?? id}.`}
        confirmLabel={action ?? "Confirm"}
        onConfirm={() => action && act.mutate(action)}
        onCancel={() => setAction(null)}
      />
    </div>
  );
}
