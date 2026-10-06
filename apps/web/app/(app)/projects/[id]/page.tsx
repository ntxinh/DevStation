"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { use } from "react";
import { ApiError, apiFetch } from "@/lib/api";
import { ErrorState } from "@/components/ErrorState";
import { formatDateTime, formatRelativeTime } from "@/lib/format";
import { GitStatusCard } from "@/features/projects/GitStatusCard";
import type { CommitInfo, GitStatus, Project } from "@/features/projects/types";

export default function ProjectDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  const project = useQuery({
    queryKey: ["projects", id],
    queryFn: () => apiFetch<Project>(`/api/projects/${id}`),
  });
  const git = useQuery({
    queryKey: ["projects", id, "git"],
    queryFn: () => apiFetch<GitStatus>(`/api/projects/${id}/git`),
    retry: false,
  });
  const commits = useQuery({
    queryKey: ["projects", id, "commits"],
    queryFn: () => apiFetch<CommitInfo[]>(`/api/projects/${id}/commits?n=10`),
    enabled: git.isSuccess,
    retry: false,
  });

  const p = project.data;
  const gitError = git.error instanceof ApiError ? git.error.message : null;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <Link href="/projects" className="text-xs text-muted hover:text-fg">← Projects</Link>
        <h1 className="text-lg font-semibold">{p?.name ?? "…"}</h1>
        {p?.tags.map((t) => (
          <span key={t} className="rounded border border-border px-1.5 py-0.5 text-[10px] text-muted">
            {t}
          </span>
        ))}
      </div>
      {project.error ? <ErrorState error={project.error} /> : null}
      {p ? (
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <div className="rounded-lg border border-border bg-panel p-3 text-xs">
            <div className="grid grid-cols-[6rem_1fr] gap-x-3 gap-y-1.5">
              <span className="text-muted">Description</span><span>{p.description ?? "—"}</span>
              <span className="text-muted">Repository</span><span className="truncate font-mono">{p.repositoryUrl ?? "—"}</span>
              <span className="text-muted">Local path</span><span className="truncate font-mono">{p.localPath ?? "—"}</span>
              <span className="text-muted">Default branch</span><span className="font-mono">{p.defaultBranch ?? "—"}</span>
              <span className="text-muted">Technology</span><span>{p.technology ?? "—"}</span>
              <span className="text-muted">Created</span><span>{formatDateTime(p.createdAt)}</span>
            </div>
          </div>
          {git.data ? <GitStatusCard status={git.data} /> : null}
          {git.isError ? (
            <div className="rounded-lg border border-border bg-panel p-3 text-xs text-muted">
              Git status unavailable{gitError ? `: ${gitError}` : ""}
            </div>
          ) : null}
        </div>
      ) : null}
      {commits.data && commits.data.length > 0 ? (
        <div className="rounded-lg border border-border bg-panel p-3">
          <h2 className="text-xs font-medium text-muted">Recent commits</h2>
          <table className="mt-2 w-full border-collapse text-xs">
            <tbody>
              {commits.data.map((c) => (
                <tr key={c.sha} className="border-b border-border/50 last:border-0">
                  <td className="py-1 pr-3 font-mono">{c.sha.slice(0, 8)}</td>
                  <td className="py-1 pr-3">{c.message}</td>
                  <td className="py-1 pr-3 text-muted">{c.author}</td>
                  <td className="py-1 text-right text-muted">{formatRelativeTime(c.when)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}
