const DOT: Record<string, string> = {
  healthy: "bg-ok",
  ok: "bg-ok",
  running: "bg-ok",
  succeeded: "bg-ok",
  connected: "bg-ok",
  queued: "bg-warn",
  warn: "bg-warn",
  degraded: "bg-warn",
  paused: "bg-warn",
  created: "bg-warn",
  configured: "bg-warn",
  unhealthy: "bg-err",
  err: "bg-err",
  error: "bg-err",
  failing: "bg-err",
  failed: "bg-err",
  dead: "bg-err",
  exited: "bg-err",
  "never-run": "bg-muted",
  notconfigured: "bg-muted",
  disabled: "bg-muted",
  cancelled: "bg-muted",
  unknown: "bg-muted",
};

export type BadgeStatus = keyof typeof DOT;

export function StatusBadge({ status }: { status: BadgeStatus }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-xs">
      <span className={`h-2 w-2 rounded-full ${DOT[status] ?? "bg-muted"}`} aria-hidden />
      <span>{status}</span>
    </span>
  );
}
