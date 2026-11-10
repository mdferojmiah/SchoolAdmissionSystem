import type { ClassSummary } from "../api/types";

export interface ClassListProps {
  classes: ClassSummary[];
  canApply: boolean;
  hasStudentId: boolean;
  applyingClassId: string | null;
  onApply(classId: string): void;
}

export function ClassList({
  classes,
  canApply,
  hasStudentId,
  applyingClassId,
  onApply,
}: ClassListProps) {
  if (classes.length === 0) {
    return <p className="empty-state">No classes are available yet.</p>;
  }

  return (
    <div className="class-list-wrapper">
      <table className="class-list">
        <caption>Classes and seat availability</caption>
        <thead>
          <tr>
            <th scope="col">Class</th>
            <th scope="col">Total</th>
            <th scope="col">Held</th>
            <th scope="col">Confirmed</th>
            <th scope="col">Available</th>
            {canApply && <th scope="col">Action</th>}
          </tr>
        </thead>
        <tbody>
          {classes.map((schoolClass) => {
            const isFull = schoolClass.availableSeats <= 0;
            const isApplying = applyingClassId === schoolClass.id;

            return (
              <tr key={schoolClass.id}>
                <th scope="row">{schoolClass.name}</th>
                <td>{schoolClass.totalSeats}</td>
                <td>{schoolClass.heldSeats}</td>
                <td>{schoolClass.confirmedSeats}</td>
                <td>{schoolClass.availableSeats}</td>
                {canApply && (
                  <td className="class-actions">
                    {isFull ? (
                      <span className="seat-full">Full</span>
                    ) : (
                      <button
                        type="button"
                        onClick={() => onApply(schoolClass.id)}
                        disabled={isApplying || !hasStudentId}
                      >
                        {isApplying ? "Applying…" : "Apply"}
                      </button>
                    )}
                    {!hasStudentId && !isFull && (
                      <span className="field-hint">
                        Enter your student ID above to apply.
                      </span>
                    )}
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
