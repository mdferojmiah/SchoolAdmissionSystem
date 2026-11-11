import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ErrorBanner } from "./ErrorBanner";

describe("ErrorBanner component", () => {
  it("renders friendly message in alert role", () => {
    render(<ErrorBanner message="No seats are available in this class." />);
    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("No seats are available in this class.");
  });

  it("calls onDismiss when dismiss button is clicked", () => {
    const onDismiss = vi.fn();
    render(<ErrorBanner message="Temporary issue" onDismiss={onDismiss} />);
    fireEvent.click(screen.getByRole("button", { name: /Dismiss/i }));
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });
});
