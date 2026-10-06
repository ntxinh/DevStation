"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { HealthBadge } from "@/features/health/HealthBadge";
import { serviceHostPort, type Service } from "@/features/services/types";
import { apiFetch } from "@/lib/api";

export default function ServicesPage() {
  const qc = useQueryClient();
  const [restartTarget, setRestartTarget] = useState<Service | null>(null);

  const { data, error, isLoading, refetch, isRefetching } = useQuery({
    queryKey: ["services"],
    queryFn: () => apiFetch<Service[]>("/api/services"),
    refetchInterval: 30_000,
  });

  const check = useMutation({
    mutationFn: (id: string) => apiFetch(`/api/services/${id}/health`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["services"] }),
  });

  const restart = useMutation({
    mutationFn: (id: string) =>
      apiFetch(`/api/services/${id}/restart?confirm=true`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["services"] }),
    onSettled: () => setRestartTarget(null),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Services</h1>
        <button
          onClick={() => refetch()}
          className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
        >
          {isRefetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.length === 0 ? <EmptyState title="No services yet" /> : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Name</th>
              <th className="py-1.5 pr-3 font-medium">Type</th>
              <th className="py-1.5 pr-3 font-medium">Host:Port</th>
              <th className="py-1.5 pr-3 font-medium">Environment</th>
              <th className="py-1.5 pr-3 font-medium">Health</th>
              <th className="py-1.5 font-medium" />
            </tr>
          </thead>
          <tbody>
            {data.map((s) => (
              <tr key={s.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3 text-fg">{s.name}</td>
                <td className="py-1.5 pr-3 text-muted">{s.type ?? "—"}</td>
                <td className="py-1.5 pr-3 font-mono text-muted">
                  {serviceHostPort(s.url)}
                  {s.url && s.url.startsWith("http") ? (
                    <>
                      {" "}
                      <a
                        href={s.url}
                        target="_blank"
                        rel="noreferrer"
                        className="text-accent hover:underline"
                      >
                        open ↗
                      </a>
                    </>
                  ) : null}
                </td>
                <td className="py-1.5 pr-3 text-muted">{s.environmentName ?? "—"}</td>
                <td className="py-1.5 pr-3"><HealthBadge status={s.status} /></td>
                <td className="py-1.5 text-right">
                  <button
                    onClick={() => check.mutate(s.id)}
                    disabled={check.isPending}
                    className="rounded border border-border px-2 py-0.5 text-muted hover:text-fg disabled:opacity-40"
                  >
                    Check
                  </button>
                  {s.containerId ? (
                    <button
                      onClick={() => setRestartTarget(s)}
                      className="ml-2 rounded border border-border px-2 py-0.5 text-muted hover:text-fg"
                    >
                      Restart
                    </button>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
      <ConfirmDialog
        open={restartTarget !== null}
        title={`Restart ${restartTarget?.name ?? "service"}`}
        description={`Restarts container ${restartTarget?.containerId ?? ""}. The service will be briefly unavailable.`}
        confirmLabel="Restart"
        onConfirm={() => restartTarget && restart.mutate(restartTarget.id)}
        onCancel={() => setRestartTarget(null)}
      />
      {restart.isError ? (
        <div className="text-xs text-err">
          {restart.error instanceof Error ? restart.error.message : "Restart failed"}
        </div>
      ) : null}
    </div>
  );
}
