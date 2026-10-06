import { apiFetch } from "@/lib/api";

export interface ExplainErrorResult {
  cause: string;
  evidence: string[];
  investigation: string[];
  suggestedFix: string;
}

export interface ExplainSqlResult {
  explanation: string;
}

export interface GenerateSqlResult {
  sql: string | null;
  explanation: string;
}

export const explainError = (body: { logText?: string; logId?: string }) =>
  apiFetch<ExplainErrorResult>("/api/ai/explain-error", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const explainSql = (body: { sql: string; connectionId?: string }) =>
  apiFetch<ExplainSqlResult>("/api/ai/explain-sql", {
    method: "POST",
    body: JSON.stringify(body),
  });

export const generateSql = (body: { question: string; connectionId: string }) =>
  apiFetch<GenerateSqlResult>("/api/ai/generate-sql", {
    method: "POST",
    body: JSON.stringify(body),
  });
