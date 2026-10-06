"use client";

import { useEffect, useRef, useState } from "react";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description: string;
  /** When set, the user must type this exact value before Confirm enables. */
  requireText?: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({
  open,
  title,
  description,
  requireText,
  confirmLabel = "Confirm",
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const [typed, setTyped] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    setTyped("");
    const el = ref.current;
    const focusable = () =>
      Array.from(el?.querySelectorAll<HTMLElement>("input, button") ?? []).filter((n) => !n.hasAttribute("disabled"));
    (el?.querySelector("input") ?? focusable()[0])?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onCancel();
      if (e.key === "Tab") {
        // ponytail: focus trap-lite — cycles input+buttons inside the dialog only
        const items = focusable();
        const first = items[0];
        const last = items[items.length - 1];
        if (!first || !last) return;
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open, onCancel]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onCancel}>
      <div
        ref={ref}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="w-full max-w-sm rounded-lg border border-border bg-panel p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-sm font-semibold">{title}</h2>
        <p className="mt-1 text-xs text-muted">{description}</p>
        {requireText ? (
          <input
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            placeholder={`Type "${requireText}" to confirm`}
            className="mt-3 w-full rounded border border-border bg-bg px-2 py-1.5 text-xs outline-none focus:border-accent"
          />
        ) : null}
        <div className="mt-4 flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="rounded border border-border px-3 py-1.5 text-xs text-muted hover:text-fg"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={requireText !== undefined && typed !== requireText}
            className="rounded bg-err px-3 py-1.5 text-xs font-medium text-white disabled:opacity-40"
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
