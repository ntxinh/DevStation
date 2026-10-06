"use client";

import { useQuery } from "@tanstack/react-query";
import { ErrorState } from "@/components/ErrorState";
import { Stat } from "@/components/Stat";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import { apiFetch } from "@/lib/api";

interface DashboardSummary {
  projects: number;
  services: number;
  containers: number;
  environmentHealth: BadgeStatus;
}

export default function Dashboard() {
  const { data, error, isLoading } = useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: () => apiFetch<DashboardSummary>("/api/dashboard/summary"),
    retry: false,
  });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Dashboard</h1>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data ? (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <Stat label="Projects" value={data.projects} />
          <Stat label="Services" value={data.services} />
          <Stat label="Containers" value={data.containers} />
          <Stat label="Environment health" value={<StatusBadge status={data.environmentHealth} />} />
        </div>
      ) : null}
    </div>
  );
}
