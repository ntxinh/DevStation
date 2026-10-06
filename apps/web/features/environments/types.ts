import type { BadgeStatus } from "@/components/StatusBadge";

export interface EnvVariable {
  key: string;
  value?: string; // omitted when secret
  secret: boolean;
}

export interface AppEnvironment {
  id: string;
  name: string;
  description: string | null;
  type: string;
  baseUrl: string | null;
  healthCheckUrl: string | null;
  gitBranch: string | null;
  gitCommit: string | null;
  deploymentProvider: string | null;
  status: string;
  variables: EnvVariable[];
  order: number;
  createdAt: string;
  updatedAt: string;
  health: BadgeStatus;
}

export interface Service {
  id: string;
  name: string;
  description: string | null;
  type: string | null;
  url: string | null;
  version: string | null;
  status: string;
}

export interface HealthResult {
  status: BadgeStatus;
  latencyMs: number | null;
  error: string | null;
  checkedAt: string;
}

export interface HealthCheck {
  id: string;
  name: string;
  kind: string;
  target: string;
  environmentId: string | null;
  serviceId: string | null;
  intervalSeconds: number;
  timeoutSeconds: number;
  enabled: boolean;
  last: HealthResult | null;
}

export interface DeploymentInfo {
  version: string | null;
  commit: string | null;
  deployedAt: string | null;
}

export interface EnvironmentDetail {
  environment: AppEnvironment;
  services: Service[];
  healthChecks: HealthCheck[];
  deployment: DeploymentInfo | null;
}

export interface ComparePair<T> {
  a: T;
  b: T;
}

export interface CompareResult {
  version: ComparePair<string | null>;
  commit: ComparePair<string | null>;
  deployedAt: ComparePair<string | null>;
  services: { name: string; inA: boolean; inB: boolean }[];
  variables: { key: string; differs: boolean; masked: boolean }[];
}
