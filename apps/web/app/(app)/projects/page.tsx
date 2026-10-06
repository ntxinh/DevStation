"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge } from "@/components/StatusBadge";
import { ProjectForm } from "@/features/projects/ProjectForm";
import type { Project } from "@/features/projects/types";
import { apiFetch } from "@/lib/api";

export default function ProjectsPage() {
  const [creating, setCreating] = useState(false);
  const { data, error, isLoading } = useQuery({
    queryKey: ["projects"],
    queryFn: () => apiFetch<Project[]>("/api/projects"),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Projects</h1>
        <button
          onClick={() => setCreating(true)}
          className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-black"
        >
          New project
        </button>
      </div>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      {data && data.length === 0 ? <EmptyState title="No projects yet" /> : null}
      {data && data.length > 0 ? (
        <table className="w-full border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-left text-muted">
              <th className="py-1.5 pr-3 font-medium">Name</th>
              <th className="py-1.5 pr-3 font-medium">Technology</th>
              <th className="py-1.5 pr-3 font-medium">Branch</th>
              <th className="py-1.5 font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {data.map((p) => (
              <tr key={p.id} className="border-b border-border/50 hover:bg-panel">
                <td className="py-1.5 pr-3">
                  <Link href={`/projects/${p.id}`} className="text-fg hover:text-accent">
                    {p.name}
                  </Link>
                </td>
                <td className="py-1.5 pr-3 text-muted">{p.technology ?? "—"}</td>
                <td className="py-1.5 pr-3 font-mono">{p.gitBranch ?? "—"}</td>
                <td className="py-1.5">
                  {p.gitDirty === null ? (
                    <span className="text-muted">—</span>
                  ) : (
                    <StatusBadge status={p.gitDirty ? "warn" : "ok"} />
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
      {creating ? <ProjectForm onClose={() => setCreating(false)} /> : null}
    </div>
  );
}
