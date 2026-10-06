export interface JobInfo {
  id: string;
  name: string;
  description: string | null;
  schedule: string | null;
  timezone: string | null;
  status: string; // healthy | failing | paused | never-run
  lastRun: string | null;
  nextRun: string | null;
  durationMs: number | null;
  successCount: number;
  failureCount: number;
}

export interface JobExecutionInfo {
  id: string;
  startedAt: string;
  endedAt: string | null;
  durationMs: number | null;
  status: string;
  error: string | null;
}

export interface JobDetail {
  job: JobInfo;
  executions: JobExecutionInfo[];
}
