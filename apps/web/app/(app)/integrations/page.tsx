"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { ErrorState } from "@/components/ErrorState";
import { StatusBadge, type BadgeStatus } from "@/components/StatusBadge";
import { GitHubPanel } from "@/features/integrations/GitHubPanel";
import type { IntegrationDto, TestResult } from "@/features/integrations/types";
import { apiFetch, ApiError } from "@/lib/api";
import { formatRelativeTime } from "@/lib/format";

interface ConnectBody {
  config: Record<string, string>;
  secrets: Record<string, string>;
}

const BADGE: Record<string, BadgeStatus> = {
  connected: "connected",
  configured: "configured",
  error: "error",
  disabled: "disabled",
  notconfigured: "notconfigured",
};

export default function IntegrationsPage() {
  const qc = useQueryClient();
  const { data, error, isLoading } = useQuery({
    queryKey: ["integrations"],
    queryFn: () => apiFetch<IntegrationDto[]>("/api/integrations"),
  });
  const [connecting, setConnecting] = useState<IntegrationDto | null>(null);
  const [disconnecting, setDisconnecting] = useState<IntegrationDto | null>(null);
  const [config, setConfig] = useState<Record<string, string>>({});
  const [secrets, setSecrets] = useState<Record<string, string>>({});
  const [formErr, setFormErr] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<Record<string, TestResult>>({});
  const [expandGitHub, setExpandGitHub] = useState(false);

  const invalidate = () =>
    qc.invalidateQueries({ queryKey: ["integrations"] });

  const connect = useMutation({
    mutationFn: ({ type, body }: { type: string; body: ConnectBody }) =>
      apiFetch<IntegrationDto>(`/api/integrations/${type}/connect`, {
        method: "POST",
        body: JSON.stringify(body),
      }),
    onSuccess: () => {
      invalidate();
      setConnecting(null);
      setFormErr(null);
    },
    onError: (e) =>
      setFormErr(e instanceof ApiError ? e.message : String(e)),
  });

  const test = useMutation({
    mutationFn: (type: string) =>
      apiFetch<TestResult>(`/api/integrations/${type}/test`, { method: "POST" }),
    onSuccess: (r, type) => {
      setTestResult((m) => ({ ...m, [type]: r }));
      invalidate();
    },
  });

  const disconnect = useMutation({
    mutationFn: (type: string) =>
      apiFetch(`/api/integrations/${type}/disconnect`, {
        method: "POST",
        body: JSON.stringify({ confirm: true }),
      }),
    onSuccess: () => {
      invalidate();
      setDisconnecting(null);
    },
  });

  function openConnect(i: IntegrationDto) {
    setConfig(Object.fromEntries((i.configKeys ?? []).map((k) => [k, ""])));
    setSecrets(Object.fromEntries((i.secretKeys ?? []).map((k) => [k, ""])));
    setFormErr(null);
    setConnecting(i);
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold">Integrations</h1>
      {isLoading ? <div className="text-xs text-muted">Loading…</div> : null}
      {error ? <ErrorState error={error} /> : null}
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        {(data ?? []).map((i) => (
          <div
            key={i.type}
            className="rounded-lg border border-border bg-panel p-3 text-xs"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="font-medium">{i.name}</span>
              <StatusBadge status={BADGE[i.status] ?? "unknown"} />
            </div>
            <div className="mt-1 flex items-center gap-3 text-muted">
              <span>{i.type}</span>
              <span>{i.hasCredentials ? "credentials stored" : "no credentials"}</span>
              <span>
                {i.lastSyncAt
                  ? `synced ${formatRelativeTime(i.lastSyncAt)}`
                  : "never synced"}
              </span>
            </div>
            <div className="mt-2 flex items-center gap-2">
              <button
                onClick={() => openConnect(i)}
                className="rounded border border-border px-2 py-0.5 hover:text-fg"
              >
                {i.hasCredentials ? "Reconfigure" : "Connect"}
              </button>
              <button
                onClick={() => test.mutate(i.type)}
                disabled={test.isPending}
                className="rounded border border-border px-2 py-0.5 hover:text-fg disabled:opacity-40"
              >
                Test
              </button>
              {i.hasCredentials ? (
                <button
                  onClick={() => setDisconnecting(i)}
                  className="rounded border border-err/50 px-2 py-0.5 text-err hover:bg-err/10"
                >
                  Disconnect
                </button>
              ) : null}
              {i.type === "github" && i.hasCredentials ? (
                <button
                  onClick={() => setExpandGitHub((v) => !v)}
                  className="ml-auto rounded border border-border px-2 py-0.5 text-muted hover:text-fg"
                >
                  {expandGitHub ? "Hide repos" : "Repos"}
                </button>
              ) : null}
            </div>
            {testResult[i.type] ? (
              <div
                className={`mt-1 ${testResult[i.type].ok ? "text-ok" : "text-err"}`}
              >
                test: {testResult[i.type].ok ? "ok" : testResult[i.type].status}
              </div>
            ) : null}
            {i.type === "github" && expandGitHub ? (
              <GitHubPanel enabled={i.hasCredentials} />
            ) : null}
          </div>
        ))}
      </div>

      {connecting ? (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
          onClick={() => setConnecting(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label={`Connect ${connecting.name}`}
            className="w-full max-w-md rounded-lg border border-border bg-panel p-4"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 className="text-sm font-semibold">
              Connect {connecting.name}
            </h2>
            <form
              className="mt-3 flex flex-col gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                const cfg = Object.fromEntries(
                  Object.entries(config).filter(([, v]) => v.trim() !== ""),
                );
                const sec = Object.fromEntries(
                  Object.entries(secrets).filter(([, v]) => v.trim() !== ""),
                );
                connect.mutate({
                  type: connecting.type,
                  body: { config: cfg, secrets: sec },
                });
              }}
            >
              {connecting.configKeys.map((k) => (
                <label key={k} className="flex flex-col gap-1 text-xs">
                  <span className="text-muted">{k}</span>
                  <input
                    value={config[k] ?? ""}
                    onChange={(e) =>
                      setConfig((c) => ({ ...c, [k]: e.target.value }))
                    }
                    className="rounded border border-border bg-bg px-2 py-1.5 outline-none focus:border-accent"
                  />
                </label>
              ))}
              {connecting.secretKeys.map((k) => (
                <label key={k} className="flex flex-col gap-1 text-xs">
                  <span className="text-muted">{k} (secret)</span>
                  <input
                    type="password"
                    value={secrets[k] ?? ""}
                    onChange={(e) =>
                      setSecrets((s) => ({ ...s, [k]: e.target.value }))
                    }
                    className="rounded border border-border bg-bg px-2 py-1.5 outline-none focus:border-accent"
                  />
                </label>
              ))}
              {connecting.configKeys.length === 0 &&
              connecting.secretKeys.length === 0 ? (
                <p className="text-xs text-muted">
                  No fields — connect just registers the integration.
                </p>
              ) : null}
              {formErr ? <div className="text-xs text-err">{formErr}</div> : null}
              <div className="mt-2 flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setConnecting(null)}
                  className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={connect.isPending}
                  className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-bg disabled:opacity-40"
                >
                  {connect.isPending ? "Connecting…" : "Connect"}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        open={disconnecting !== null}
        title={`Disconnect ${disconnecting?.name ?? ""}`}
        description="Stored credentials and configuration are removed."
        confirmLabel="Disconnect"
        onConfirm={() =>
          disconnecting && disconnect.mutate(disconnecting.type)
        }
        onCancel={() => setDisconnecting(null)}
      />
    </div>
  );
}
