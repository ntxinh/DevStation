import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";

const MAP: Record<string, BadgeStatus> = {
  healthy: "healthy",
  degraded: "degraded",
  unhealthy: "unhealthy",
  unknown: "unknown",
};

export function HealthBadge({ status }: { status: string | null | undefined }) {
  return <StatusBadge status={MAP[status ?? "unknown"] ?? "unknown"} />;
}
