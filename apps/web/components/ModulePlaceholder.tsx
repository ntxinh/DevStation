export function ModulePlaceholder({ name }: { name: string }) {
  return (
    <div className="flex flex-col gap-1">
      <h1 className="text-lg font-semibold">{name}</h1>
      <p className="text-xs text-muted">This module is not implemented yet.</p>
    </div>
  );
}
