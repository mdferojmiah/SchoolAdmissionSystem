import { useEffect, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { useStudent } from "../context/StudentContext";
import { STUDENT_ID_LENGTH } from "../utils/studentId";

export function StudentIdBar() {
  const { studentId, hasStudentId, saveStudentId, clearStudentId } =
    useStudent();
  const [value, setValue] = useState(studentId);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    setValue(studentId);
  }, [studentId]);

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setValue(
      event.target.value.replace(/\D/g, "").slice(0, STUDENT_ID_LENGTH),
    );
    setError(null);
    setSaved(false);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const result = saveStudentId(value);

    if (result === "saved") {
      setError(null);
      setSaved(true);
      return;
    }

    setSaved(false);
    setError(
      result === "invalid"
        ? "Enter exactly 17 digits."
        : "Saved for this session only. This browser is blocking storage.",
    );
  }

  function handleClear() {
    const persisted = clearStudentId();
    setValue("");
    setSaved(false);
    setError(
      persisted
        ? null
        : "Cleared for this session only. This browser is blocking storage.",
    );
  }

  return (
    <section className="student-id-bar" aria-label="Student identity">
      <form onSubmit={handleSubmit} noValidate>
        <label htmlFor="student-id">Birth Registration Number (17 digits)</label>
        <div className="student-id-fields">
          <input
            id="student-id"
            name="studentId"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            maxLength={STUDENT_ID_LENGTH}
            pattern="[0-9]*"
            value={value}
            onChange={handleChange}
            aria-invalid={error !== null}
            aria-describedby={error ? "student-id-error" : undefined}
          />
          <button type="submit">Save</button>
          {hasStudentId && (
            <button type="button" onClick={handleClear}>
              Clear
            </button>
          )}
        </div>
        {error && (
          <p className="field-error" id="student-id-error" role="alert">
            {error}
          </p>
        )}
        {!error && saved && <p className="field-status">Saved.</p>}
      </form>
    </section>
  );
}
