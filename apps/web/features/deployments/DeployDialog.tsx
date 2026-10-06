"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import type { AppEnvironment } from "@/features/environments/types";
import type { Deployment } from "@/features/deployments/types";
import { apiFetch } from "@/lib/api";

interface ProjectPick {
  id: string;
  name: string;
}

interface DeployDialogProps {
  open: boolean;
  onClose: () => void;
  onDeployed: (d: Deployment) => void;
}

export function DeployDialog({ open, onClose, onDeployed }: DeployDialogProps) {
  const [environmentId, setEnvironmentId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [commit, setCommit] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const { data: envs } = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<AppEnvironment[]>("/api/environments"),
    enabled: open,
  });
  const { data: projects } = useQuery({
    queryKey: ["projects"],
    queryFn: () => apiFetch<ProjectPick[]>("/api/projects"),
    enabled: open,
  });

  if (!open) return null;

  const env = envs?.find((e) => e.id === environmentId);
  const prod = env?.type === "production";

  async function submit() {
    if (!environmentId) return;
    setPending(true);
    setError(null);
    try {
      const d = await apiFetch<Deployment>(
        `/api/deployments?environmentId=${environmentId}&confirm=true`,
        {
          method: "POST",
          body: JSON.stringify({
            projectId: projectId || null,
            commit: commit || null,
          }),
        },
      );
      onDeployed(d);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Deploy failed");
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Deploy"
        className="w-full max-w-sm rounded-lg border border-border bg-panel p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold">Deploy</h2>
        <div className="mt-3 flex flex-col gap-3 text-xs">
          <label className="flex flex-col gap-1">
            <span className="text-muted">Environment</span>
            <select
              value={environmentId}
              onChange={(e) => setEnvironmentId(e.target.value)}
              className="rounded border border-border bg-bg px-2 py-1.5 outline-none focus:border-accent"
            >
              <option value="">Select environment…</option>
              {(envs ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name} ({e.type})
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-muted">Project</span>
            <select
              value={projectId}
              onChange={(e) => setProjectId(e.target.value)}
              className="rounded border border-border bg-bg px-2 py-1.5 outline-none focus:border-accent"
            >
              <option value="">— none —</option>
              {(projects ?? []).map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-muted">Commit (sha or ref)</span>
            <input
              value={commit}
              onChange={(e) => setCommit(e.target.value)}
              placeholder="default branch"
              className="rounded border border-border bg-bg px-2 py-1.5 font-mono outline-none focus:border-accent"
            />
          </label>
          {prod ? (
            <p className="rounded border border-err/50 bg-err/10 px-2 py-1.5 text-err">
              Production deployment — this will go live.
            </p>
          ) : null}
          {error ? <p className="text-err">{error}</p> : null}
          <div className="flex justify-end gap-2">
            <button
              onClick={onClose}
              className="rounded border border-border px-3 py-1.5 text-muted hover:text-fg"
            >
              Cancel
            </button>
            <button
              onClick={submit}
              disabled={!environmentId || pending}
              className={`rounded px-3 py-1.5 font-medium text-white disabled:opacity-40 ${
                prod ? "bg-err" : "bg-accent"
              }`}
            >
              {pending ? "Deploying…" : "Deploy"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
