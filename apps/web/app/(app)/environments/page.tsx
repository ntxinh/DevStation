"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { HealthBadge } from "@/features/health/HealthBadge";
import type { AppEnvironment } from "@/features/environments/types";
import { apiFetch } from "@/lib/api";

export default function EnvironmentsPage() {
  const { data, error, isLoading } = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<AppEnvironment[]>("/api/environments"),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Environments</h1>
        <Link href="/environments/compare" className="text-xs text-accent hover:underline">
          Compare →
        </Link>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.length === 0 ? <EmptyState title="No environments yet" /> : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Name</th>
              <th className="py-1.5 pr-3 font-medium">Type</th>
              <th className="py-1.5 pr-3 font-medium">Base URL</th>
              <th className="py-1.5 font-medium">Health</th>
            </tr>
          </thead>
          <tbody>
            {data.map((e) => (
              <tr key={e.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3">
                  <Link href={`/environments/${e.id}`} className="text-fg hover:text-accent">
                    {e.name}
                  </Link>
                </td>
                <td className="py-1.5 pr-3 text-muted">{e.type}</td>
                <td className="py-1.5 pr-3 font-mono text-muted">{e.baseUrl ?? "—"}</td>
                <td className="py-1.5"><HealthBadge status={e.health} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
