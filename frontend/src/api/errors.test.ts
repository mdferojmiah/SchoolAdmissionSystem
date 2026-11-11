import { describe, it, expect } from "vitest";
import { ApiError, toFriendlyMessage } from "./errors";

describe("toFriendlyMessage", () => {
  it("maps NO_SEATS error code to friendly message", () => {
    const error = new ApiError(409, "NO_SEATS", "Class is full");
    expect(toFriendlyMessage(error)).toBe("No seats are available in this class.");
  });

  it("maps DUPLICATE_APPLICATION error code to friendly message", () => {
    const error = new ApiError(409, "DUPLICATE_APPLICATION");
    expect(toFriendlyMessage(error)).toBe("You have already applied to this class.");
  });

  it("maps HOLD_EXPIRED error code to friendly message", () => {
    const error = new ApiError(409, "HOLD_EXPIRED");
    expect(toFriendlyMessage(error)).toBe("Your hold has expired. Please apply again.");
  });

  it("maps EXPIRED error code to friendly message", () => {
    const error = new ApiError(409, "EXPIRED");
    expect(toFriendlyMessage(error)).toBe("Your hold has expired. Please apply again.");
  });

  it("maps NOT_OWNER error code to friendly message", () => {
    const error = new ApiError(403, "NOT_OWNER");
    expect(toFriendlyMessage(error)).toBe("This application belongs to a different student.");
  });

  it("maps NETWORK_ERROR error code to friendly message", () => {
    const error = new ApiError(0, "NETWORK_ERROR");
    expect(toFriendlyMessage(error)).toBe("Cannot reach the server. Please try again.");
  });

  it("maps HTTP status 400 with unknown code to validation message", () => {
    const error = new ApiError(400, "CUSTOM_BAD_REQUEST");
    expect(toFriendlyMessage(error)).toBe("Please check the entered information.");
  });

  it("maps unknown error to safe generic message without leaking internals", () => {
    expect(toFriendlyMessage(new Error("Fatal crash in DB connection"))).toBe(
      "Something went wrong. Please try again."
    );
  });
});
