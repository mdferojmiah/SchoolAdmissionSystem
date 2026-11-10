export type HoldStatus = "HELD" | "CONFIRMED" | "EXPIRED" | "CANCELLED";

export interface CreateClassInput {
  name: string;
  totalSeats: number;
}

export interface CreatedClass {
  id: string;
  name: string;
  totalSeats: number;
}

export interface ClassSummary {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface HoldCreated {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface HoldStatusResponse {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface HoldSummary {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
}

export interface ConfirmResponse {
  holdId: string;
  status: "CONFIRMED";
}

export interface ProblemDetailsBody {
  status?: number;
  code?: string;
  message?: string;
  title?: string;
  detail?: string;
}
