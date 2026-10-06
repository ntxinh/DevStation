export interface Deployment {
  id: string;
  projectId: string | null;
  projectName: string | null;
  environmentId: string | null;
  environmentName: string | null;
  commit: string | null;
  version: string | null;
  status: string; // queued | running | succeeded | failed | cancelled
  durationMs: number | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  logs: string[];
}
