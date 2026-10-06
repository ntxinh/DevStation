"use client";

import { standardSchemaResolver } from "@hookform/resolvers/standard-schema";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiFetch } from "@/lib/api";
import type { AppEnvironment } from "@/features/environments/types";
import type { DbConnection } from "./types";

const schema = z.object({
  name: z.string().trim().min(1, "required").max(200),
  kind: z.string(),
  host: z.string().trim().min(1, "required"),
  port: z.number().int().min(1).max(65535),
  database: z.string().trim().min(1, "required"),
  username: z.string().trim().min(1, "required"),
  password: z.string(),
  environmentId: z.string(),
  readOnly: z.boolean(),
  maxRows: z.number().int().min(1).max(10000),
  timeoutSeconds: z.number().int().min(1).max(300),
});
type FormValues = z.infer<typeof schema>;

const INPUT =
  "w-full rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent";

function Field({ label, error, children }: {
  label: string; error?: string; children: React.ReactNode;
}) {
  return (
    <label className="flex flex-col gap-1 text-xs">
      <span className="text-muted">{label}</span>
      {children}
      {error ? <span className="text-err">{error}</span> : null}
    </label>
  );
}

export function ConnectionForm({
  conn,
  onClose,
}: {
  conn?: DbConnection; // edit mode when set
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const envs = useQuery({
    queryKey: ["environments"],
    queryFn: () => apiFetch<AppEnvironment[]>("/api/environments"),
  });
  const { register, handleSubmit, formState: { errors } } = useForm<FormValues>({
    resolver: standardSchemaResolver(schema),
    defaultValues: conn
      ? {
          name: conn.name, kind: conn.kind, host: conn.host, port: conn.port,
          database: conn.database, username: conn.username, password: "",
          environmentId: conn.environmentId ?? "", readOnly: conn.readOnly,
          maxRows: conn.maxRows, timeoutSeconds: conn.timeoutSeconds,
        }
      : {
          name: "", kind: "postgresql", host: "", port: 5432, database: "",
          username: "", password: "", environmentId: "", readOnly: true,
          maxRows: 1000, timeoutSeconds: 30,
        },
  });

  const save = useMutation({
    mutationFn: (v: FormValues) => {
      const body: Record<string, unknown> = {
        name: v.name, kind: v.kind, host: v.host, port: v.port,
        database: v.database, username: v.username,
        environmentId: v.environmentId || null,
        unlinkEnvironment: !v.environmentId,
        readOnly: v.readOnly, maxRows: v.maxRows,
        timeoutSeconds: v.timeoutSeconds,
      };
      if (v.password) body.password = v.password;
      return conn
        ? apiFetch<DbConnection>(`/api/databases/connections/${conn.id}`, {
            method: "PATCH", body: JSON.stringify(body) })
        : apiFetch<DbConnection>("/api/databases/connections", {
            method: "POST", body: JSON.stringify(body) });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["db-connections"] });
      onClose();
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        role="dialog" aria-modal="true"
        aria-label={conn ? "Edit connection" : "New connection"}
        className="w-full max-w-md rounded-lg border border-border bg-panel p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold">
          {conn ? "Edit connection" : "New connection"}
        </h2>
        <form
          onSubmit={handleSubmit((v) => save.mutate(v))}
          className="mt-3 grid grid-cols-2 gap-3"
        >
          <Field label="Name" error={errors.name?.message}>
            <input className={INPUT} {...register("name")} />
          </Field>
          <Field label="Kind">
            <select className={INPUT} {...register("kind")}>
              <option value="postgresql">postgresql</option>
              <option value="sqlserver">sqlserver</option>
            </select>
          </Field>
          <Field label="Host" error={errors.host?.message}>
            <input className={INPUT} {...register("host")} />
          </Field>
          <Field label="Port" error={errors.port?.message}>
            <input type="number" className={INPUT}
              {...register("port", { valueAsNumber: true })} />
          </Field>
          <Field label="Database" error={errors.database?.message}>
            <input className={INPUT} {...register("database")} />
          </Field>
          <Field label="Username" error={errors.username?.message}>
            <input className={INPUT} {...register("username")} />
          </Field>
          <Field
            label={conn ? "Password (blank = keep)" : "Password"}
            error={errors.password?.message}
          >
            <input type="password" className={INPUT} autoComplete="off"
              {...register("password")} />
          </Field>
          <Field label="Environment">
            <select className={INPUT} {...register("environmentId")}>
              <option value="">— none —</option>
              {(envs.data ?? []).map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name} ({e.type})
                </option>
              ))}
            </select>
          </Field>
          <Field label="Max rows" error={errors.maxRows?.message}>
            <input type="number" className={INPUT}
              {...register("maxRows", { valueAsNumber: true })} />
          </Field>
          <Field label="Timeout (s)" error={errors.timeoutSeconds?.message}>
            <input type="number" className={INPUT}
              {...register("timeoutSeconds", { valueAsNumber: true })} />
          </Field>
          <label className="col-span-2 flex items-center gap-2 text-xs text-muted">
            <input type="checkbox" {...register("readOnly")} />
            Read-only (blocks all writes, even with confirm)
          </label>
          {save.error ? (
            <div className="col-span-2 text-xs text-err">
              {save.error.message}
            </div>
          ) : null}
          <div className="col-span-2 flex justify-end gap-2">
            <button type="button" onClick={onClose}
              className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg">
              Cancel
            </button>
            <button type="submit" disabled={save.isPending}
              className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-bg disabled:opacity-40">
              {save.isPending ? "Saving…" : "Save"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
