"use client";

import { standardSchemaResolver } from "@hookform/resolvers/standard-schema";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiFetch } from "@/lib/api";
import type { Project } from "./types";

const schema = z.object({
  name: z.string().trim().min(1, "required").max(200),
  description: z.string(),
  repositoryUrl: z
    .string()
    .refine((s) => !s || /^https?:\/\//.test(s), "must be http(s)"),
  localPath: z
    .string()
    .refine((s) => !s || s.startsWith("/"), "must be absolute"),
  defaultBranch: z.string(),
  technology: z.string(),
  tags: z.string(), // comma-separated in the form
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

export function ProjectForm({ onClose }: { onClose: () => void }) {
  const router = useRouter();
  const qc = useQueryClient();
  const { register, handleSubmit, formState: { errors } } = useForm<FormValues>({
    resolver: standardSchemaResolver(schema),
    defaultValues: {
      name: "", description: "", repositoryUrl: "", localPath: "",
      defaultBranch: "main", technology: "", tags: "",
    },
  });

  const create = useMutation({
    mutationFn: (v: FormValues) =>
      apiFetch<Project>("/api/projects", {
        method: "POST",
        body: JSON.stringify({
          name: v.name,
          description: v.description || null,
          repositoryUrl: v.repositoryUrl || null,
          localPath: v.localPath || null,
          defaultBranch: v.defaultBranch || null,
          technology: v.technology || null,
          tags: v.tags ? v.tags.split(",").map((t) => t.trim()).filter(Boolean) : [],
        }),
      }),
    onSuccess: (p) => {
      qc.invalidateQueries({ queryKey: ["projects"] });
      onClose();
      router.push(`/projects/${p.id}`);
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        role="dialog" aria-modal="true" aria-label="New project"
        className="w-full max-w-md rounded-lg border border-border bg-panel p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold">New project</h2>
        <form onSubmit={handleSubmit((v) => create.mutate(v))} className="mt-3 flex flex-col gap-3">
          <Field label="Name" error={errors.name?.message}>
            <input {...register("name")} className={INPUT} autoFocus />
          </Field>
          <Field label="Description">
            <input {...register("description")} className={INPUT} />
          </Field>
          <Field label="Repository URL" error={errors.repositoryUrl?.message}>
            <input {...register("repositoryUrl")} className={INPUT} placeholder="https://…" />
          </Field>
          <Field label="Local path" error={errors.localPath?.message}>
            <input {...register("localPath")} className={INPUT} placeholder="/home/you/repo" />
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Default branch">
              <input {...register("defaultBranch")} className={INPUT} />
            </Field>
            <Field label="Technology">
              <input {...register("technology")} className={INPUT} placeholder="dotnet, node…" />
            </Field>
          </div>
          <Field label="Tags (comma-separated)">
            <input {...register("tags")} className={INPUT} />
          </Field>
          {create.error ? (
            <div className="text-xs text-err">{create.error.message}</div>
          ) : null}
          <div className="mt-1 flex justify-end gap-2">
            <button type="button" onClick={onClose}
              className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg">
              Cancel
            </button>
            <button type="submit" disabled={create.isPending}
              className="rounded bg-accent px-3 py-1.5 text-xs font-medium text-white disabled:opacity-40">
              {create.isPending ? "Creating…" : "Create"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
