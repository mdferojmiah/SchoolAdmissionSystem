import { useState } from "react";
import { api } from "../api/client";
import { toFriendlyMessage } from "../api/errors";
import type { ClassSummary } from "../api/types";
import { ClassList } from "../components/ClassList";
import { useErrorBanner } from "../context/ErrorBannerContext";
import { useRole } from "../context/RoleContext";
import { useStudent } from "../context/StudentContext";
import { usePolling } from "../hooks/usePolling";

export function ClassesPage() {
  const { isAdmin } = useRole();
  const { studentId, hasStudentId } = useStudent();
  const { showError } = useErrorBanner();
  const { data, error, isLoading, refresh } = usePolling<ClassSummary[]>(() =>
    api.listClasses(),
  );
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  async function handleApply(classId: string) {
    if (!hasStudentId || !studentId) {
      showError("Enter your 17-digit student ID before applying.");
      return;
    }

    setNotice(null);
    setApplyingClassId(classId);

    try {
      const hold = await api.applyToClass(classId, studentId);
      setNotice(
        `Seat held. Your application is active until ${new Date(
          hold.expiresAt,
        ).toLocaleString()}.`,
      );
      await refresh();
    } catch (caught) {
      showError(toFriendlyMessage(caught));
    } finally {
      setApplyingClassId(null);
    }
  }

  return (
    <section className="panel" aria-label="Classes">
      <h2>Classes</h2>

      {isLoading && data === null && (
        <p className="loading-state">Loading classes…</p>
      )}

      {error !== null && (
        <div className="poll-error" role="alert">
          <span>{toFriendlyMessage(error)}</span>
          <button
            type="button"
            onClick={() => {
              void refresh();
            }}
          >
            Retry
          </button>
        </div>
      )}

      {data !== null && (
        <ClassList
          classes={data}
          canApply={!isAdmin}
          hasStudentId={hasStudentId}
          applyingClassId={applyingClassId}
          onApply={(classId) => {
            void handleApply(classId);
          }}
        />
      )}

      {notice !== null && (
        <p className="field-status" role="status">
          {notice}
        </p>
      )}
    </section>
  );
}
