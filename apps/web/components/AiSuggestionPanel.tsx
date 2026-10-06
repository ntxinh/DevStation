"use client";

import { useState } from "react";
import type { ExplainErrorResult } from "@/features/ai/api";

interface Props {
  title: string;
  loading?: boolean;
  error?: string | null;
  /** error-explanation shape */
  result?: ExplainErrorResult | null;
  /** free text (explain-sql / generate-sql explanation) */
  text?: string | null;
  /** generated SQL — rendered as copyable code, never run */
  sql?: string | null;
  onApplySql?: (sql: string) => void;
}

/// Dismissible AI suggestion panel — always carries the verify disclaimer.
export function AiSuggestionPanel({
  title, loading, error, result, text, sql, onApplySql,
}: Props) {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;

  return (
    <div className="rounded border border-accent/40 bg-panel px-3 py-2 text-xs">
      <div className="mb-1 flex items-center justify-between">
        <span className="font-medium text-accent">{title}</span>
        <button
          onClick={() => setDismissed(true)}
          aria-label="Dismiss suggestion"
          className="text-muted hover:text-fg"
        >
          ✕
        </button>
      </div>

      {loading ? <div className="text-muted">Thinking…</div> : null}
      {error ? <div className="text-err">{error}</div> : null}

      {result ? (
        <div className="flex flex-col gap-1.5">
          <div>
            <span className="text-muted">Likely cause: </span>
            {result.cause}
          </div>
          {result.evidence.length > 0 ? (
            <ul className="list-disc pl-4">
              {result.evidence.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          ) : null}
          {result.investigation.length > 0 ? (
            <div>
              <div className="text-muted">Investigate:</div>
              <ul className="list-disc pl-4">
                {result.investigation.map((e, i) => <li key={i}>{e}</li>)}
              </ul>
            </div>
          ) : null}
          {result.suggestedFix ? (
            <div>
              <span className="text-muted">Suggested fix: </span>
              {result.suggestedFix}
            </div>
          ) : null}
        </div>
      ) : null}

      {text ? <div className="whitespace-pre-wrap">{text}</div> : null}

      {sql ? (
        <div>
          <pre className="mt-1 overflow-auto rounded border border-border bg-bg p-2 font-mono">
            {sql}
          </pre>
          {onApplySql ? (
            <button
              onClick={() => onApplySql(sql)}
              className="mt-1 rounded border border-border px-2 py-0.5 text-muted hover:text-fg"
            >
              Copy to editor
            </button>
          ) : null}
        </div>
      ) : null}

      <div className="mt-2 border-t border-border/50 pt-1 text-[10px] italic text-muted">
        AI suggestion — verify before applying
      </div>
    </div>
  );
}
