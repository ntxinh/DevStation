"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";

interface AuditEvent {
  id: string;
  timestamp: string;
  user: string | null;
  action: string;
  resource: string | null;
  resourceId: string | null;
  environmentId: string | null;
  result: string;
  metadata: unknown;
}

const RESULT_CLASS: Record<string, string> = {
  ok: "text-ok",
  denied: "text-warn",
  error: "text-err",
};

export default function AuditPage() {
  const [action, setAction] = useState("");
  const [resource, setResource] = useState("");

  const params = new URLSearchParams();
  if (action) params.set("action", action);
  if (resource) params.set("resource", resource);

  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["audit", action, resource],
    queryFn: () => apiFetch<AuditEvent[]>(`/api/audit?${params}`),
    refetchInterval: 30_000,
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Audit Log</h1>
        <button
          onClick={() => refetch()}
          className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
        >
          {isRefetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      <div className="flex gap-2">
        <input
          value={action}
          onChange={(e) => setAction(e.target.value)}
          placeholder="Filter by action (e.g. project.create)"
          aria-label="Filter by action"
          className="w-64 rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        />
        <input
          value={resource}
          onChange={(e) => setResource(e.target.value)}
          placeholder="Filter by resource"
          aria-label="Filter by resource"
          className="w-48 rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
        />
      </div>

      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.length === 0 ? (
        <EmptyState title="No audit events">
          Actions against projects, environments, services and more are
          recorded here.
        </EmptyState>
      ) : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Time</th>
              <th className="py-1.5 pr-3 font-medium">User</th>
              <th className="py-1.5 pr-3 font-medium">Action</th>
              <th className="py-1.5 pr-3 font-medium">Resource</th>
              <th className="py-1.5 pr-3 font-medium">Result</th>
              <th className="py-1.5 font-medium">Metadata</th>
            </tr>
          </thead>
          <tbody>
            {data.map((e) => (
              <tr key={e.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1 pr-3 whitespace-nowrap text-muted">
                  {formatDateTime(e.timestamp)}
                </td>
                <td className="py-1 pr-3">{e.user ?? "—"}</td>
                <td className="py-1 pr-3 font-mono">{e.action}</td>
                <td className="py-1 pr-3 text-muted">
                  {e.resource ?? "—"}
                  {e.resourceId ? `/${e.resourceId.slice(0, 8)}` : ""}
                </td>
                <td className={`py-1 pr-3 ${RESULT_CLASS[e.result] ?? ""}`}>
                  {e.result}
                </td>
                <td className="py-1 font-mono text-muted">
                  {e.metadata ? JSON.stringify(e.metadata) : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
