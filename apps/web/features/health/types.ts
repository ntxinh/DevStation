import type { HealthCheck, HealthResult } from "@/features/environments/types";

export interface HealthOverviewItem {
  check: HealthCheck;
  last: HealthResult | null;
  uptime24h: number;
}

export interface HealthOverview {
  checks: HealthOverviewItem[];
}
