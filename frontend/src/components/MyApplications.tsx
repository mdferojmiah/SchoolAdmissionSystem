import { useCallback, useState } from "react";
import { api } from "../api/client";
import { toFriendlyMessage } from "../api/errors";
import type { HoldSummary } from "../api/types";
import { useErrorBanner } from "../context/ErrorBannerContext";
import { useStudent } from "../context/StudentContext";
import { usePolling } from "../hooks/usePolling";
import { ApplicationCard } from "./ApplicationCard";

export function MyApplications() {
  const { studentId, hasStudentId } = useStudent();
  const { showError } = useErrorBanner();
  const [processingHoldId, setProcessingHoldId] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const fetchHolds = useCallback(async (): Promise<HoldSummary[]> => {
    if (!hasStudentId || !studentId) {
      return [];
    }
    return api.getMyHolds(studentId);
  }, [hasStudentId, studentId]);

  const { data, error, isLoading, refresh } = usePolling<HoldSummary[]>(
    fetchHolds,
    4000,
  );

  if (!hasStudentId) {
    return null;
  }

  async function handleConfirm(holdId: string) {
    if (processingHoldId) {
      return;
    }

    setProcessingHoldId(holdId);
    setActionNotice(null);

    try {
      await api.confirmHold(holdId, studentId);
      setActionNotice("Admission fee paid successfully. Seat confirmed!");
      await refresh();
    } catch (caught) {
      showError(toFriendlyMessage(caught));
    } finally {
      setProcessingHoldId(null);
    }
  }

  async function handleCancel(holdId: string) {
    if (processingHoldId) {
      return;
    }

    setProcessingHoldId(holdId);
    setActionNotice(null);

    try {
      await api.cancelHold(holdId, studentId);
      setActionNotice("Application cancelled. Seat released.");
      await refresh();
    } catch (caught) {
      showError(toFriendlyMessage(caught));
    } finally {
      setProcessingHoldId(null);
    }
  }

  return (
    <section className="panel my-applications" aria-label="My Applications">
      <h2>My Applications</h2>

      {isLoading && data === null && (
        <p className="loading-state">Loading applications…</p>
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

      {actionNotice && (
        <p className="field-status" role="status">
          {actionNotice}
        </p>
      )}

      {data !== null && data.length === 0 && (
        <p className="empty-state">
          No applications found for student ID <code>{studentId}</code>.
        </p>
      )}

      {data !== null && data.length > 0 && (
        <div className="applications-grid">
          {data.map((hold) => (
            <ApplicationCard
              key={hold.holdId}
              hold={hold}
              isProcessing={processingHoldId === hold.holdId}
              onConfirm={handleConfirm}
              onCancel={handleCancel}
            />
          ))}
        </div>
      )}
    </section>
  );
}
