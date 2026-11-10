export const STORAGE_KEYS = {
  studentId: "school-admission.studentId",
  role: "school-admission.role",
  adminSignedIn: "school-admission.adminSignedIn",
} as const;

export function readStorage(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writeStorage(key: string, value: string | null): boolean {
  try {
    if (value === null) {
      window.localStorage.removeItem(key);
    } else {
      window.localStorage.setItem(key, value);
    }

    return true;
  } catch {
    return false;
  }
}
