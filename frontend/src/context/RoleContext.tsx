import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { readStorage, STORAGE_KEYS, writeStorage } from "../utils/storage";

export type Role = "guardian" | "admin";

export interface RoleContextValue {
  role: Role;
  isAdmin: boolean;
  storageWarning: boolean;
  selectGuardian(): void;
  selectAdmin(): void;
}

const RoleContext = createContext<RoleContextValue | null>(null);

export function RoleProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<Role>(() =>
    readStorage(STORAGE_KEYS.role) === "admin" ? "admin" : "guardian",
  );
  const [storageWarning, setStorageWarning] = useState(false);

  const value = useMemo<RoleContextValue>(
    () => ({
      role,
      storageWarning,
      isAdmin: role === "admin",
      selectGuardian() {
        setRole("guardian");
        setStorageWarning(!writeStorage(STORAGE_KEYS.role, "guardian"));
      },
      selectAdmin() {
        setRole("admin");
        setStorageWarning(!writeStorage(STORAGE_KEYS.role, "admin"));
      },
    }),
    [role, storageWarning],
  );

  return <RoleContext.Provider value={value}>{children}</RoleContext.Provider>;
}

export function useRole(): RoleContextValue {
  const context = useContext(RoleContext);
  if (!context) {
    throw new Error("useRole must be used inside RoleProvider.");
  }

  return context;
}
