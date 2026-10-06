export interface Project {
  id: string;
  name: string;
  description: string | null;
  repositoryUrl: string | null;
  localPath: string | null;
  defaultBranch: string | null;
  technology: string | null;
  tags: string[];
  createdAt: string;
  updatedAt: string;
  gitBranch: string | null;
  gitDirty: boolean | null;
}

export interface GitStatus {
  branch: string;
  commitSha: string | null;
  commitMessage: string | null;
  dirty: boolean;
  ahead: number;
  behind: number;
}

export interface CommitInfo {
  sha: string;
  message: string;
  author: string;
  when: string;
}
