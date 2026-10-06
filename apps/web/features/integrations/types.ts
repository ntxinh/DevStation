export interface IntegrationDto {
  id: string | null;
  name: string;
  type: string;
  status: string; // notconfigured|configured|connected|error|disabled
  config: Record<string, string> | null;
  hasCredentials: boolean;
  configKeys: string[];
  secretKeys: string[];
  lastSyncAt: string | null;
  updatedAt: string | null;
}

export type ConnectResult = IntegrationDto;
export interface TestResult {
  ok: boolean;
  status: string;
}

export interface GitHubRepo {
  id: number;
  name: string;
  full_name: string;
  private: boolean;
  description: string | null;
  html_url: string;
  pushed_at: string | null;
}

export interface GitHubPullRequest {
  number: number;
  title: string;
  user: { login: string } | null;
  draft: boolean;
  html_url: string;
  created_at: string;
  userNode: { login: string } | null;
}

export interface GitHubWorkflowRun {
  id: number;
  name: string;
  status: string;
  conclusion: string | null;
  head_branch: string;
  html_url: string;
  created_at: string;
}
