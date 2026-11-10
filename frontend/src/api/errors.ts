export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message?: string) {
    super(message ?? "Request failed.");
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

const CODE_MESSAGES: Record<string, string> = {
  NETWORK_ERROR: "Cannot reach the server. Please try again.",
  NO_SEATS: "No seats are available in this class.",
  DUPLICATE_APPLICATION: "You have already applied to this class.",
  HOLD_EXPIRED: "Your hold has expired. Please apply again.",
  EXPIRED: "Your hold has expired. Please apply again.",
  HOLD_CANCELLED: "This application has been cancelled.",
  CANCELLED: "This application has been cancelled.",
  NOT_OWNER: "This application belongs to a different student.",
  FORBIDDEN: "This application belongs to a different student.",
  CLASS_NOT_FOUND: "We could not find what you were looking for.",
  HOLD_NOT_FOUND: "We could not find what you were looking for.",
  NOT_FOUND: "We could not find what you were looking for.",
  VALIDATION_ERROR: "Please check the entered information.",
  INTERNAL_ERROR: "Something went wrong. Please try again.",
};

function messageForStatus(status: number): string {
  if (status === 0) {
    return "Cannot reach the server. Please try again.";
  }

  if (status === 400) {
    return "Please check the entered information.";
  }

  if (status === 403) {
    return "This application belongs to a different student.";
  }

  if (status === 404) {
    return "We could not find what you were looking for.";
  }

  return "Something went wrong. Please try again.";
}

export function toFriendlyMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const byCode = CODE_MESSAGES[error.code];
    if (byCode) {
      return byCode;
    }

    return messageForStatus(error.status);
  }

  return "Something went wrong. Please try again.";
}
