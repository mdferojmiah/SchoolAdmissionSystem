import { ApiError } from "./errors";
import type {
  ClassSummary,
  ConfirmResponse,
  CreateClassInput,
  CreatedClass,
  HoldCreated,
  HoldStatusResponse,
  HoldSummary,
  ProblemDetailsBody,
} from "./types";

const API_URL = (import.meta.env.VITE_API_URL ?? "http://localhost:8080").replace(
  /\/+$/,
  "",
);
const DEFAULT_NETWORK_MESSAGE = "Cannot reach the server. Please try again.";

let lastServerDate: Date | null = null;

interface RequestOptions {
  method?: "GET" | "POST" | "DELETE";
  studentId?: string;
  body?: unknown;
  signal?: AbortSignal;
}

function codeForStatus(status: number): string {
  switch (status) {
    case 400:
    case 401:
      return "VALIDATION_ERROR";
    case 403:
      return "NOT_OWNER";
    case 404:
      return "NOT_FOUND";
    case 409:
      return "CONFLICT";
    default:
      return "INTERNAL_ERROR";
  }
}

async function parseProblemDetails(response: Response): Promise<ProblemDetailsBody> {
  try {
    const parsed: unknown = await response.json();
    if (parsed && typeof parsed === "object") {
      return parsed as ProblemDetailsBody;
    }
  } catch {
    return {};
  }

  return {};
}

async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const headers: Record<string, string> = {
    Accept: "application/json",
  };

  if (options.studentId) {
    headers["X-Student-Id"] = options.studentId;
  }

  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch {
    throw new ApiError(0, "NETWORK_ERROR", DEFAULT_NETWORK_MESSAGE);
  }

  rememberServerDate(response);

  if (!response.ok) {
    const body = await parseProblemDetails(response);
    throw new ApiError(
      response.status,
      body.code ?? codeForStatus(response.status),
      body.message,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  try {
    return (await response.json()) as T;
  } catch {
    throw new ApiError(
      response.status,
      "INVALID_RESPONSE",
      "The server sent an unexpected response.",
    );
  }
}

function rememberServerDate(response: Response): void {
  const header = response.headers.get("Date");
  if (!header) {
    return;
  }

  const parsed = new Date(header);
  if (!Number.isNaN(parsed.getTime())) {
    lastServerDate = parsed;
  }
}

export function getLastServerDate(): Date | null {
  return lastServerDate;
}

export const api = {
  listClasses(): Promise<ClassSummary[]> {
    return request<ClassSummary[]>("/classes");
  },

  getClass(classId: string): Promise<ClassSummary> {
    return request<ClassSummary>(`/classes/${encodeURIComponent(classId)}`);
  },

  createClass(input: CreateClassInput): Promise<CreatedClass> {
    return request<CreatedClass>("/classes", {
      method: "POST",
      body: input,
    });
  },

  applyToClass(classId: string, studentId: string): Promise<HoldCreated> {
    return request<HoldCreated>(
      `/classes/${encodeURIComponent(classId)}/holds`,
      {
        method: "POST",
        studentId,
      },
    );
  },

  getHold(holdId: string): Promise<HoldStatusResponse> {
    return request<HoldStatusResponse>(
      `/holds/${encodeURIComponent(holdId)}`,
    );
  },

  confirmHold(holdId: string, studentId: string): Promise<ConfirmResponse> {
    return request<ConfirmResponse>(
      `/holds/${encodeURIComponent(holdId)}/confirm`,
      {
        method: "POST",
        studentId,
      },
    );
  },

  cancelHold(holdId: string, studentId: string): Promise<void> {
    return request<void>(`/holds/${encodeURIComponent(holdId)}`, {
      method: "DELETE",
      studentId,
    });
  },

  getMyHolds(studentId: string): Promise<HoldSummary[]> {
    return request<HoldSummary[]>("/me/holds", { studentId });
  },
};
