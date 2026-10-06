import { LuCircleAlert } from "react-icons/lu";

export function ErrorState({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : "Something went wrong";
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-border bg-panel py-12 text-center">
      <LuCircleAlert className="h-6 w-6 text-err" aria-hidden />
      <div className="text-sm font-medium">Could not load data</div>
      <div className="max-w-md text-xs text-muted">{message}</div>
    </div>
  );
}
