import { LuInbox } from "react-icons/lu";
import type { ReactNode } from "react";

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border py-12 text-center">
      <LuInbox className="h-6 w-6 text-muted" aria-hidden />
      <div className="text-sm font-medium">{title}</div>
      {children ? <div className="text-xs text-muted">{children}</div> : null}
    </div>
  );
}
