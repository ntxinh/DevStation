const DOT: Record<string, string> = {
  healthy: "bg-ok",
  ok: "bg-ok",
  degraded: "bg-warn",
  warn: "bg-warn",
  unhealthy: "bg-err",
  err: "bg-err",
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
