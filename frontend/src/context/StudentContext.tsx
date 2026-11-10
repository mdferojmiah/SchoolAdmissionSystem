import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { readStorage, STORAGE_KEYS, writeStorage } from "../utils/storage";
import { isValidStudentId, STUDENT_ID_LENGTH } from "../utils/studentId";

export interface StudentContextValue {
  studentId: string;
  hasStudentId: boolean;
  saveStudentId(raw: string): boolean;
  clearStudentId(): void;
}

const StudentContext = createContext<StudentContextValue | null>(null);

export function StudentProvider({ children }: { children: ReactNode }) {
  const [studentId, setStudentId] = useState(() => {
    const stored = readStorage(STORAGE_KEYS.studentId);
    return stored && isValidStudentId(stored) ? stored : "";
  });

  const value = useMemo<StudentContextValue>(
    () => ({
      studentId,
      hasStudentId: studentId.length === STUDENT_ID_LENGTH,
      saveStudentId(raw: string) {
        const next = raw.trim();
        if (!isValidStudentId(next)) {
          return false;
        }

        setStudentId(next);
        writeStorage(STORAGE_KEYS.studentId, next);
        return true;
      },
      clearStudentId() {
        setStudentId("");
        writeStorage(STORAGE_KEYS.studentId, null);
      },
    }),
    [studentId],
  );

  return (
    <StudentContext.Provider value={value}>{children}</StudentContext.Provider>
  );
}

export function useStudent(): StudentContextValue {
  const context = useContext(StudentContext);
  if (!context) {
    throw new Error("useStudent must be used inside StudentProvider.");
  }

  return context;
}
