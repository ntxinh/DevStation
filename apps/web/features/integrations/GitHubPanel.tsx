"use client";

import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";
import type {
  GitHubPullRequest,
  GitHubRepo,
  GitHubWorkflowRun,
} from "./types";
import { useState } from "react";

/** Repos → pulls + workflow runs drill-down for the connected GitHub card. */
export function GitHubPanel({ enabled }: { enabled: boolean }) {
  const [repo, setRepo] = useState<string | null>(null); // "owner/name"

  const repos = useQuery({
    queryKey: ["integrations", "github", "repos"],
    queryFn: () => apiFetch<GitHubRepo[]>("/api/integrations/github/repos"),
    enabled,
  });

  const [owner, name] = repo?.split("/") ?? [];
  const pulls = useQuery({
    queryKey: ["integrations", "github", "pulls", repo],
    queryFn: () =>
      apiFetch<GitHubPullRequest[]>(
        `/api/integrations/github/${owner}/${name}/pulls`,
      ),
    enabled: enabled && repo !== null,
  });
  const runs = useQuery({
    queryKey: ["integrations", "github", "runs", repo],
    queryFn: () =>
      apiFetch<GitHubWorkflowRun[]>(
        `/api/integrations/github/${owner}/${name}/workflow-runs`,
      ),
    enabled: enabled && repo !== null,
  });

  if (repos.isLoading) return <div className="text-xs text-muted">Loading repos…</div>;
  if (repos.error)
    return (
      <div className="text-xs text-err">
        {(repos.error as Error).message}
      </div>
    );

  return (
    <div className="mt-3 flex flex-col gap-3 border-t border-border pt-3">
      <div>
        <div className="mb-1 text-[11px] font-medium uppercase tracking-wide text-muted">
          Repositories
        </div>
        <ul className="flex flex-col gap-0.5">
          {(repos.data ?? []).map((r) => (
            <li key={r.id}>
              <button
                onClick={() => setRepo(r.full_name)}
                className={`w-full rounded px-2 py-1 text-left text-xs hover:bg-bg ${
                  repo === r.full_name ? "bg-bg text-fg" : "text-muted"
                }`}
              >
                {r.full_name}
                {r.private ? " · private" : ""}
                {r.pushed_at
                  ? ` · pushed ${formatRelativeTime(r.pushed_at)}`
                  : ""}
              </button>
            </li>
          ))}
        </ul>
      </div>

      {repo ? (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
          <div>
            <div className="mb-1 text-[11px] font-medium uppercase tracking-wide text-muted">
              Open pull requests — {repo}
            </div>
            <ul className="flex flex-col gap-1 text-xs">
              {(pulls.data ?? []).map((p) => (
                <li key={p.number}>
                  <a
                    href={p.html_url}
                    target="_blank"
                    className="text-accent hover:underline"
                  >
                    #{p.number} {p.title}
                  </a>
                  <span className="text-muted"> — {p.user?.login}</span>
                </li>
              ))}
              {pulls.data?.length === 0 ? (
                <li className="text-muted">No open PRs</li>
              ) : null}
            </ul>
          </div>
          <div>
            <div className="mb-1 text-[11px] font-medium uppercase tracking-wide text-muted">
              Workflow runs — {repo}
            </div>
            <ul className="flex flex-col gap-1 text-xs">
              {(runs.data ?? []).map((r) => (
                <li key={r.id} className="flex items-center gap-2">
                  <span
                    className={`h-1.5 w-1.5 rounded-full ${
                      r.conclusion === "success"
                        ? "bg-ok"
                        : r.conclusion === "failure"
                          ? "bg-err"
                          : "bg-warn"
                    }`}
                    aria-hidden
                  />
                  <a
                    href={r.html_url}
                    target="_blank"
                    rel="noreferrer"
                    className="text-accent hover:underline"
                  >
                    {r.name}
                  </a>
                  <span className="text-muted">
                    {r.conclusion ?? r.status} · {r.head_branch}
                  </span>
                </li>
              ))}
              {runs.data?.length === 0 ? (
                <li className="text-muted">No runs</li>
              ) : null}
            </ul>
          </div>
        </div>
      ) : null}
    </div>
  );
}
