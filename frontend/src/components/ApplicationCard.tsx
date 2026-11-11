import type { HoldSummary } from "../api/types";
import { useCountdown } from "../hooks/useCountdown";

export interface ApplicationCardProps {
  hold: HoldSummary;
  isProcessing: boolean;
  onConfirm(holdId: string): Promise<void>;
  onCancel(holdId: string): Promise<void>;
}

export function ApplicationCard({
  hold,
  isProcessing,
  onConfirm,
  onCancel,
}: ApplicationCardProps) {
  const { display, isExpired } = useCountdown(hold.expiresAt);

  // If status is HELD and timer expired locally, display EXPIRED (Rule U2)
  const effectiveStatus =
    hold.status === "HELD" && isExpired ? "EXPIRED" : hold.status;

  const isHeld = hold.status === "HELD" && !isExpired;

  const statusClass = `status-badge status-${effectiveStatus.toLowerCase()}`;

  return (
    <article className="application-card" aria-labelledby={`hold-title-${hold.holdId}`}>
      <div className="card-header">
        <div>
          <h3 id={`hold-title-${hold.holdId}`} className="card-class-name">
            {hold.className || "Class Admission"}
          </h3>
          <p className="card-ids">
            <span>Hold ID: <code>{hold.holdId}</code></span>
            <span>Class ID: <code>{hold.classId}</code></span>
          </p>
        </div>
        <span className={statusClass}>{effectiveStatus}</span>
      </div>

      <div className="card-body">
        {isHeld && (
          <div className="countdown-box" role="timer" aria-live="polite">
            <span className="countdown-label">Time remaining to pay:</span>
            <span className="countdown-timer">{display}</span>
          </div>
        )}

        {effectiveStatus === "EXPIRED" && (
          <p className="status-explanation status-expired-text">
            This seat hold has expired. The seat has been released for others.
          </p>
        )}

        {effectiveStatus === "CONFIRMED" && (
          <p className="status-explanation status-confirmed-text">
            Admission confirmed. Your seat is secured!
          </p>
        )}

        {effectiveStatus === "CANCELLED" && (
          <p className="status-explanation status-cancelled-text">
            This application was cancelled.
          </p>
        )}
      </div>

      {hold.status === "HELD" && (
        <div className="card-actions">
          <button
            type="button"
            className="btn-pay"
            disabled={isExpired || isProcessing}
            onClick={() => onConfirm(hold.holdId)}
          >
            {isProcessing ? "Processing…" : "Pay Admission Fee"}
          </button>
          <button
            type="button"
            className="btn-cancel"
            disabled={isExpired || isProcessing}
            onClick={() => onCancel(hold.holdId)}
          >
            {isProcessing ? "Processing…" : "Cancel"}
          </button>
        </div>
      )}
    </article>
  );
}
