"use client";

import { useEffect, type ReactNode } from "react";
import { Nav } from "@/components/Nav";
import { TokenGate } from "@/components/TokenGate";
import { Topbar } from "@/components/Topbar";
import { CommandPalette } from "@/components/CommandPalette";
import { useUiStore } from "@/stores/ui";

export default function AppLayout({ children }: { children: ReactNode }) {
  const { paletteOpen, setPaletteOpen } = useUiStore();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen(true);
      }
      if (e.key === "Escape") setPaletteOpen(false);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [setPaletteOpen]);

  return (
    <TokenGate>
      <div className="flex h-screen">
        <Nav />
        <div className="flex min-w-0 flex-1 flex-col">
          <Topbar />
          <main className="flex-1 overflow-auto p-4">{children}</main>
        </div>
      </div>
      {paletteOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center bg-black/60 pt-32"
          onClick={() => setPaletteOpen(false)}
        >
          <div onClick={(e) => e.stopPropagation()}>
            <CommandPalette onClose={() => setPaletteOpen(false)} />
          </div>
        </div>
      ) : null}
    </TokenGate>
  );
}
