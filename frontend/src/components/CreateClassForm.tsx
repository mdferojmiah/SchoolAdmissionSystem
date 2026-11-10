import { useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { api } from "../api/client";
import { toFriendlyMessage } from "../api/errors";
import type { CreateClassInput, CreatedClass } from "../api/types";

export interface CreateClassFormProps {
  onCreated(created: CreatedClass): void;
}

interface FieldErrors {
  name?: string;
  totalSeats?: string;
}

const CONTROL_CHARACTERS = /[\u0000-\u001f\u007f]/;
const POSITIVE_INTEGER = /^[1-9]\d*$/;
const MAX_TOTAL_SEATS = 2147483647;

export function CreateClassForm({ onCreated }: CreateClassFormProps) {
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [created, setCreated] = useState(false);

  function handleNameChange(event: ChangeEvent<HTMLInputElement>) {
    setName(event.target.value);
    setFieldErrors((previous) => ({ ...previous, name: undefined }));
    setServerError(null);
    setCreated(false);
  }

  function handleSeatsChange(event: ChangeEvent<HTMLInputElement>) {
    setSeats(event.target.value);
    setFieldErrors((previous) => ({ ...previous, totalSeats: undefined }));
    setServerError(null);
    setCreated(false);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmedName = name.trim();
    const trimmedSeats = seats.trim();
    const errors: FieldErrors = {};

    if (trimmedName.length === 0) {
      errors.name = "Enter a class name.";
    } else if (trimmedName.length > 200) {
      errors.name = "Use 200 characters or fewer.";
    } else if (CONTROL_CHARACTERS.test(trimmedName)) {
      errors.name = "Class name must not contain control characters.";
    }

    const parsedSeats = Number(trimmedSeats);
    if (
      !POSITIVE_INTEGER.test(trimmedSeats) ||
      parsedSeats > MAX_TOTAL_SEATS
    ) {
      errors.totalSeats = "Enter a positive whole number of seats.";
    }

    setFieldErrors(errors);

    if (Object.keys(errors).length > 0) {
      return;
    }

    const input: CreateClassInput = {
      name: trimmedName,
      totalSeats: parsedSeats,
    };

    setSubmitting(true);
    setServerError(null);
    setCreated(false);

    try {
      const result = await api.createClass(input);
      setName("");
      setSeats("");
      setCreated(true);
      onCreated(result);
    } catch (caught) {
      setServerError(toFriendlyMessage(caught));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="create-class" onSubmit={handleSubmit} noValidate>
      <h2>Create Class</h2>

      <div className="create-class-field">
        <label htmlFor="class-name">Class name</label>
        <input
          id="class-name"
          name="name"
          type="text"
          maxLength={200}
          autoComplete="off"
          value={name}
          onChange={handleNameChange}
          aria-invalid={fieldErrors.name ? true : undefined}
          aria-describedby={
            fieldErrors.name ? "class-name-error" : undefined
          }
        />
        {fieldErrors.name && (
          <p className="field-error" id="class-name-error" role="alert">
            {fieldErrors.name}
          </p>
        )}
      </div>

      <div className="create-class-field">
        <label htmlFor="class-seats">Total seats</label>
        <input
          id="class-seats"
          name="totalSeats"
          type="number"
          min="1"
          step="1"
          inputMode="numeric"
          value={seats}
          onChange={handleSeatsChange}
          aria-invalid={fieldErrors.totalSeats ? true : undefined}
          aria-describedby={
            fieldErrors.totalSeats ? "class-seats-error" : undefined
          }
        />
        {fieldErrors.totalSeats && (
          <p className="field-error" id="class-seats-error" role="alert">
            {fieldErrors.totalSeats}
          </p>
        )}
      </div>

      {serverError && (
        <p className="field-error" role="alert">
          {serverError}
        </p>
      )}

      {created && (
        <p className="field-status" role="status">
          Class created.
        </p>
      )}

      <button type="submit" disabled={submitting}>
        {submitting ? "Creating…" : "Create Class"}
      </button>
    </form>
  );
}
