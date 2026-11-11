import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ErrorBoundary } from "./ErrorBoundary";

function Bomb(): never {
  throw new Error("Unexpected crash!");
}

describe("ErrorBoundary component (Rule U5)", () => {
  it("renders friendly fallback UI when a child crashes instead of blank page", () => {
    // Suppress console.error in test output
    const consoleSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>
    );

    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(screen.getByText("Something went wrong.")).toBeInTheDocument();
    expect(screen.getByText("Please reload the page and try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Reload page/i })).toBeInTheDocument();

    consoleSpy.mockRestore();
  });
});
