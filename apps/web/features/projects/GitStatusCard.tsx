"use client";

import { StatusBadge } from "@/components/StatusBadge";
import type { GitStatus } from "./types";

export function GitStatusCard({ status }: { status: GitStatus }) {
  return (
    <div className="rounded-lg border border-border bg-panel p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-muted">Git</span>
        <StatusBadge status={status.dirty ? "warn" : "ok"} />
      </div>
      <div className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
        <span className="text-muted">Branch</span>
        <span className="font-mono">{status.branch}</span>
        <span className="text-muted">Commit</span>
        <span className="truncate font-mono" title={status.commitSha ?? ""}>
          {status.commitSha?.slice(0, 8) ?? "—"}
        </span>
        <span className="text-muted">Message</span>
        <span className="truncate">{status.commitMessage ?? "—"}</span>
        <span className="text-muted">Ahead / behind</span>
        <span className="font-mono">↑{status.ahead} ↓{status.behind}</span>
      </div>
    </div>
  );
}
