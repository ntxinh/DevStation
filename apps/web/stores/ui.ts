import { create } from "zustand";

interface UiState {
  paletteOpen: boolean;
  sidebarOpen: boolean;
  selectedEnvironmentId: string | null;
  setPaletteOpen: (open: boolean) => void;
  toggleSidebar: () => void;
  selectEnvironment: (id: string | null) => void;
}

export const useUiStore = create<UiState>((set) => ({
  paletteOpen: false,
  sidebarOpen: true,
  selectedEnvironmentId: null,
  setPaletteOpen: (paletteOpen) => set({ paletteOpen }),
  toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
  selectEnvironment: (selectedEnvironmentId) => set({ selectedEnvironmentId }),
}));
