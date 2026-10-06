"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { IconType } from "react-icons";
import {
  LuBoxes,
  LuClipboardList,
  LuDatabase,
  LuFileText,
  LuFolderGit2,
  LuHeartPulse,
  LuLayers,
  LuLayoutDashboard,
  LuPlug,
  LuRocket,
  LuServer,
  LuSettings,
  LuTimer,
} from "react-icons/lu";
import { useUiStore } from "@/stores/ui";

// Spec §37 order — do not reorder.
export const NAV_ITEMS: { href: string; label: string; icon: IconType }[] = [
  { href: "/dashboard", label: "Dashboard", icon: LuLayoutDashboard },
  { href: "/projects", label: "Projects", icon: LuFolderGit2 },
  { href: "/environments", label: "Environments", icon: LuLayers },
  { href: "/services", label: "Services", icon: LuServer },
  { href: "/containers", label: "Containers", icon: LuBoxes },
  { href: "/databases", label: "Databases", icon: LuDatabase },
  { href: "/jobs", label: "Jobs", icon: LuTimer },
  { href: "/deployments", label: "Deployments", icon: LuRocket },
  { href: "/logs", label: "Logs", icon: LuFileText },
  { href: "/health", label: "Health", icon: LuHeartPulse },
  { href: "/integrations", label: "Integrations", icon: LuPlug },
  { href: "/audit", label: "Audit Log", icon: LuClipboardList },
  { href: "/settings", label: "Settings", icon: LuSettings },
];

export function Nav() {
  const pathname = usePathname();
  const sidebarOpen = useUiStore((s) => s.sidebarOpen);
  return (
    <nav className={`flex shrink-0 flex-col border-r border-border bg-panel ${sidebarOpen ? "w-52" : "w-12"}`}>
      <div className="flex h-12 items-center border-b border-border px-3 text-sm font-bold tracking-wide">
        {sidebarOpen ? "DevStation" : "DS"}
      </div>
      {NAV_ITEMS.map(({ href, label, icon: Icon }) => {
        const active = pathname === href || pathname.startsWith(href + "/");
        return (
          <Link
            key={href}
            href={href}
            title={label}
            className={`flex items-center gap-2.5 px-3 py-2 text-xs ${
              active ? "bg-bg text-accent" : "text-muted hover:bg-bg hover:text-fg"
            }`}
          >
            <Icon className="h-4 w-4 shrink-0" aria-hidden />
            {sidebarOpen ? label : null}
          </Link>
        );
      })}
    </nav>
  );
}
