"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { use } from "react";
import { ErrorState } from "@/components/ErrorState";
import { HealthBadge } from "@/features/health/HealthBadge";
import type { EnvironmentDetail } from "@/features/environments/types";
import { apiFetch } from "@/lib/api";
import { formatDateTime, formatRelativeTime } from "@/lib/format";

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <span className="text-muted">{label}</span>
      <span>{children}</span>
    </>
  );
}

export default function EnvironmentDetailPage({
  params,
}: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data, error } = useQuery({
    queryKey: ["environments", id],
    queryFn: () => apiFetch<EnvironmentDetail>(`/api/environments/${id}`),
  });

  const e = data?.environment;
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <Link href="/environments" className="text-xs text-muted hover:text-fg">← Environments</Link>
        <h1 className="text-lg font-semibold">{e?.name ?? "…"}</h1>
        {e ? <HealthBadge status={e.health} /> : null}
      </div>
      {error ? <ErrorState error={error} /> : null}
      {e ? (
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <div className="grid grid-cols-[8rem_1fr] gap-x-3 gap-y-1.5">
              <Row label="Type">{e.type}</Row>
              <Row label="Status">{e.status}</Row>
              <Row label="Description">{e.description ?? "—"}</Row>
              <Row label="Base URL"><span className="font-mono">{e.baseUrl ?? "—"}</span></Row>
              <Row label="Health URL"><span className="font-mono">{e.healthCheckUrl ?? "—"}</span></Row>
              <Row label="Branch"><span className="font-mono">{e.gitBranch ?? "—"}</span></Row>
              <Row label="Commit"><span className="font-mono">{e.gitCommit?.slice(0, 12) ?? "—"}</span></Row>
              <Row label="Provider">{e.deploymentProvider ?? "—"}</Row>
              <Row label="Deployed">
                {data.deployment
                  ? `${data.deployment.version ?? "?"} · ${formatDateTime(data.deployment.deployedAt)}`
                  : "—"}
              </Row>
              <Row label="Updated">{formatDateTime(e.updatedAt)}</Row>
            </div>
          </div>
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <h2 className="text-xs font-medium text-muted">Variables</h2>
            {e.variables.length === 0 ? (
              <p className="mt-2 text-muted">None</p>
            ) : (
              <table className="mt-2 w-full border-collapse">
                <tbody>
                  {e.variables.map((v) => (
                    <tr key={v.key} className="border-b border-border/50 last:border-0">
                      <td className="py-1 pr-3 font-mono">{v.key}</td>
                      <td className="py-1 font-mono text-muted">
                        {v.secret ? "••••••" : (v.value ?? "—")}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : null}
      {data && data.healthChecks.length > 0 ? (
        <div className="rounded-lg border border-border bg-panel p-3 text-xs">
          <h2 className="text-xs font-medium text-muted">Health checks</h2>
          <table className="mt-2 w-full border-collapse">
            <tbody>
              {data.healthChecks.map((h) => (
                <tr key={h.id} className="border-b border-border/50 last:border-0">
                  <td className="py-1 pr-3">{h.name}</td>
                  <td className="py-1 pr-3 font-mono text-muted">{h.kind} · {h.target}</td>
                  <td className="py-1 pr-3"><HealthBadge status={h.last?.status} /></td>
                  <td className="py-1 pr-3 text-muted">
                    {h.last?.latencyMs != null ? `${h.last.latencyMs}ms` : "—"}
                  </td>
                  <td className="py-1 text-right text-muted">
                    {h.last ? formatRelativeTime(h.last.checkedAt) : "never"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      {data && data.services.length > 0 ? (
        <div className="rounded-lg border border-border bg-panel p-3 text-xs">
          <h2 className="text-xs font-medium text-muted">Services</h2>
          <table className="mt-2 w-full border-collapse">
            <tbody>
              {data.services.map((s) => (
                <tr key={s.id} className="border-b border-border/50 last:border-0">
                  <td className="py-1 pr-3">{s.name}</td>
                  <td className="py-1 pr-3 font-mono text-muted">{s.url ?? "—"}</td>
                  <td className="py-1 pr-3 font-mono">{s.version ?? "—"}</td>
                  <td className="py-1 text-right"><HealthBadge status={s.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
