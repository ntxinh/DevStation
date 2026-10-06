"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { AiSuggestionPanel } from "@/components/AiSuggestionPanel";
import { explainSql, generateSql } from "@/features/ai/api";
import { EmptyState } from "@/components/EmptyState";
import { ErrorState } from "@/components/ErrorState";
import { apiFetch, ApiError } from "@/lib/api";
import { ConnectionForm } from "@/features/databases/ConnectionForm";
import { SchemaTree } from "@/features/databases/SchemaTree";
import { SqlEditor } from "@/features/databases/SqlEditor";
import { ResultGrid } from "@/features/databases/ResultGrid";
import {
  QueryHistoryPanel,
  SavedQueriesPanel,
} from "@/features/databases/QueryHistoryPanel";
import type {
  DbConnection,
  ExecuteResponse,
  QueryResult,
} from "@/features/databases/types";

interface RunError {
  code: string;
  message: string;
}

export default function DatabasesPage() {
  const qc = useQueryClient();
  const conns = useQuery({
    queryKey: ["db-connections"],
    queryFn: () => apiFetch<DbConnection[]>("/api/databases/connections"),
  });

  const [connId, setConnId] = useState<string | null>(null);
  const [sql, setSql] = useState("");
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<QueryResult | null>(null);
  const [runErr, setRunErr] = useState<RunError | null>(null);
  const [confirmRun, setConfirmRun] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [editing, setEditing] = useState<DbConnection | "new" | null>(null);
  const [sideTab, setSideTab] = useState<"history" | "saved">("history");
  const [question, setQuestion] = useState("");
  const aiExplain = useMutation({ mutationFn: explainSql });
  const aiGenerate = useMutation({ mutationFn: generateSql });
  const abort = useRef<AbortController | null>(null);

  const conn = conns.data?.find((c) => c.id === connId) ?? null;
  const prodLinked = conn?.environmentType === "production";

  async function run(confirm: boolean) {
    if (!connId || !sql.trim() || running) return;
    setRunning(true);
    setRunErr(null);
    setResult(null);
    abort.current = new AbortController();
    try {
      const res = await apiFetch<ExecuteResponse>(
        `/api/databases/connections/${connId}/execute${confirm ? "?confirm=true" : ""}`,
        { method: "POST", body: JSON.stringify({ sql }),
          signal: abort.current.signal },
      );
      setResult(res.result);
      qc.invalidateQueries({ queryKey: ["db-history", connId] });
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") {
        setRunErr({ code: "CANCELLED", message: "query cancelled" });
      } else if (e instanceof ApiError) {
        if (e.code === "CONFIRMATION_REQUIRED") setConfirmRun(true);
        setRunErr({ code: e.code, message: e.message });
      } else {
        setRunErr({ code: "ERROR", message: e instanceof Error ? e.message : String(e) });
      }
    } finally {
      setRunning(false);
    }
  }

  const delConn = useMutation({
    mutationFn: () =>
      apiFetch(`/api/databases/connections/${connId}?confirm=true`,
        { method: "DELETE" }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["db-connections"] });
      setConnId(null);
      setResult(null);
    },
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Databases</h1>
        <button
          onClick={() => setEditing("new")}
          className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-bg"
        >
          New connection
        </button>
      </div>

      {conns.error ? <ErrorState error={conns.error} /> : null}
      {conns.data?.length === 0 ? (
        <EmptyState title="No database connections">
          Add a connection to start querying.
        </EmptyState>
      ) : null}

      <div className="grid grid-cols-[200px_1fr_220px] gap-3">
        {/* left: connections + schema */}
        <div className="flex flex-col gap-2">
          {(conns.data ?? []).map((c) => (
            <div key={c.id}>
              <button
                onClick={() => setConnId(c.id)}
                className={`w-full rounded border px-2 py-1.5 text-left text-xs ${
                  connId === c.id
                    ? "border-accent bg-panel text-fg"
                    : "border-border text-muted hover:text-fg"
                }`}
              >
                <div className="font-medium">{c.name}</div>
                <div className="mt-0.5 flex gap-1 text-[10px]">
                  <span>{c.kind}</span>
                  {c.readOnly ? <span className="text-warn">RO</span> : null}
                  {c.environmentType === "production"
                    ? <span className="text-err">PROD</span>
                    : c.environmentType
                      ? <span>{c.environmentType}</span>
                      : null}
                </div>
                <div className="truncate text-[10px] opacity-70">
                  {c.host}:{c.port}/{c.database}
                </div>
              </button>
              {connId === c.id ? (
                <div className="mt-1 flex gap-1 text-[10px]">
                  <button onClick={() => setEditing(c)}
                    className="rounded border border-border px-1.5 py-0.5 text-muted hover:text-fg">
                    edit
                  </button>
                  <button onClick={() => setConfirmDelete(true)}
                    className="rounded border border-border px-1.5 py-0.5 text-muted hover:text-err">
                    delete
                  </button>
                </div>
              ) : null}
              {connId === c.id ? (
                <div className="mt-1 rounded border border-border/50 p-1.5">
                  <SchemaTree connectionId={c.id} onInsert={setSql} />
                </div>
              ) : null}
            </div>
          ))}
        </div>

        {/* center: editor + results */}
        <div className="flex flex-col gap-2">
          {!conn ? (
            <div className="text-xs text-muted">
              Select a connection to write SQL.
            </div>
          ) : (
            <>
              {prodLinked ? (
                <div className="rounded border border-err/50 bg-err/10 px-2 py-1 text-xs text-err">
                  Linked to a production environment — all writes are blocked.
                </div>
              ) : null}
              <SqlEditor value={sql} onChange={setSql} onRun={() => run(false)} />
              <div className="flex items-center gap-2">
                <button
                  onClick={() => run(false)}
                  disabled={running || !sql.trim()}
                  className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-bg disabled:opacity-40"
                >
                  {running ? "Running…" : "Run (Ctrl+Enter)"}
                </button>
                {running ? (
                  <button
                    onClick={() => abort.current?.abort()}
                    className="rounded bg-err px-3 py-1.5 text-xs font-medium text-white"
                  >
                    Cancel
                  </button>
                ) : null}
                {conn.readOnly ? (
                  <span className="text-[10px] text-muted">
                    read-only connection
                  </span>
                ) : null}
                <button
                  onClick={() => aiExplain.mutate(
                    { sql, connectionId: conn.id })}
                  disabled={aiExplain.isPending || !sql.trim()}
                  className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg disabled:opacity-40"
                >
                  Explain SQL
                </button>
                <input
                  value={question}
                  onChange={(e) => setQuestion(e.target.value)}
                  placeholder="Ask for a query…"
                  aria-label="Ask for a query"
                  className="w-44 rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && question.trim())
                      aiGenerate.mutate(
                        { question, connectionId: conn.id });
                  }}
                />
                <button
                  onClick={() => aiGenerate.mutate(
                    { question, connectionId: conn.id })}
                  disabled={aiGenerate.isPending || !question.trim()}
                  className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg disabled:opacity-40"
                >
                  Ask
                </button>
              </div>
              {aiExplain.isPending || aiExplain.data || aiExplain.error ? (
                <AiSuggestionPanel
                  title="Explain SQL"
                  loading={aiExplain.isPending}
                  error={aiExplain.error ? String(aiExplain.error) : null}
                  text={aiExplain.data?.explanation ?? null}
                />
              ) : null}
              {aiGenerate.isPending || aiGenerate.data || aiGenerate.error ? (
                <AiSuggestionPanel
                  title="Generated SQL"
                  loading={aiGenerate.isPending}
                  error={aiGenerate.error ? String(aiGenerate.error) : null}
                  text={aiGenerate.data?.explanation ?? null}
                  sql={aiGenerate.data?.sql ?? null}
                  onApplySql={setSql}
                />
              ) : null}
              {runErr ? (
                <div className="rounded border border-err/50 bg-err/10 px-2 py-1.5 text-xs">
                  <span className="font-mono font-semibold text-err">
                    {runErr.code}
                  </span>
                  <span className="ml-2 text-fg">{runErr.message}</span>
                </div>
              ) : null}
              {result ? (
                <>
                  <ResultGrid result={result} />
                  <div className="text-[10px] text-muted">
                    {result.rowCount} rows · {result.durationMs} ms
                    {result.truncated ? (
                      <span className="ml-1 rounded bg-warn/20 px-1 text-warn">
                        truncated
                      </span>
                    ) : null}
                  </div>
                </>
              ) : null}
            </>
          )}
        </div>

        {/* right: history + saved */}
        <div className="flex flex-col overflow-hidden rounded border border-border">
          <div className="flex border-b border-border text-xs">
            {(["history", "saved"] as const).map((t) => (
              <button
                key={t}
                onClick={() => setSideTab(t)}
                className={`flex-1 px-2 py-1.5 capitalize ${
                  sideTab === t
                    ? "bg-panel text-fg"
                    : "text-muted hover:text-fg"
                }`}
              >
                {t}
              </button>
            ))}
          </div>
          <div className="max-h-96 overflow-auto">
            {!connId ? (
              <div className="p-2 text-xs text-muted">select a connection</div>
            ) : sideTab === "history" ? (
              <QueryHistoryPanel connectionId={connId} onLoad={setSql} />
            ) : (
              <SavedQueriesPanel
                connectionId={connId}
                sql={sql}
                onLoad={setSql}
              />
            )}
          </div>
        </div>
      </div>

      {editing ? (
        <ConnectionForm
          conn={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
        />
      ) : null}

      <ConfirmDialog
        open={confirmRun}
        title="Run dangerous statement?"
        description="This statement is classified as dangerous (DDL/admin). Run it anyway?"
        confirmLabel="Run anyway"
        onCancel={() => setConfirmRun(false)}
        onConfirm={() => {
          setConfirmRun(false);
          setRunErr(null);
          run(true);
        }}
      />
      <ConfirmDialog
        open={confirmDelete}
        title="Delete connection?"
        description="Deletes the connection, its saved queries, history, and stored password."
        confirmLabel="Delete"
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => {
          setConfirmDelete(false);
          delConn.mutate();
        }}
      />
    </div>
  );
}
