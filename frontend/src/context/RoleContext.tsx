import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { readStorage, STORAGE_KEYS, writeStorage } from "../utils/storage";

export type Role = "guardian" | "admin";

export interface RoleContextValue {
  role: Role;
  isAdmin: boolean;
  adminSignedIn: boolean;
  storageWarning: boolean;
  selectGuardian(): void;
  selectAdmin(): void;
  signIn(email: string, password: string): boolean;
  signOut(): void;
}

const RoleContext = createContext<RoleContextValue | null>(null);

const ADMIN_EMAIL = import.meta.env.VITE_ADMIN_EMAIL ?? "admin@school.test";
const ADMIN_PASSWORD = import.meta.env.VITE_ADMIN_PASSWORD ?? "Adm1n!Demo";

export function RoleProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<Role>(() =>
    readStorage(STORAGE_KEYS.role) === "admin" ? "admin" : "guardian",
  );
  const [adminSignedIn, setAdminSignedIn] = useState(
    () => readStorage(STORAGE_KEYS.adminSignedIn) === "true",
  );
  const [storageWarning, setStorageWarning] = useState(false);

  const value = useMemo<RoleContextValue>(
    () => ({
      role,
      adminSignedIn,
      storageWarning,
      isAdmin: role === "admin" && adminSignedIn,
      selectGuardian() {
        setRole("guardian");
        setStorageWarning(!writeStorage(STORAGE_KEYS.role, "guardian"));
      },
      selectAdmin() {
        setRole("admin");
        setStorageWarning(!writeStorage(STORAGE_KEYS.role, "admin"));
      },
      signIn(email: string, password: string): boolean {
        const candidateEmail = email.trim();

        if (
          !candidateEmail ||
          !password ||
          !ADMIN_EMAIL ||
          !ADMIN_PASSWORD
        ) {
          return false;
        }

        if (candidateEmail !== ADMIN_EMAIL || password !== ADMIN_PASSWORD) {
          return false;
        }

        setAdminSignedIn(true);
        setStorageWarning(
          !writeStorage(STORAGE_KEYS.adminSignedIn, "true"),
        );
        return true;
      },
      signOut() {
        setAdminSignedIn(false);
        setRole("guardian");
        setStorageWarning(
          !(
            writeStorage(STORAGE_KEYS.adminSignedIn, "false") &&
            writeStorage(STORAGE_KEYS.role, "guardian")
          ),
        );
      },
    }),
    [role, adminSignedIn, storageWarning],
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
