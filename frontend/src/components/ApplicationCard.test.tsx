import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ApplicationCard } from "./ApplicationCard";
import type { HoldSummary } from "../api/types";

describe("ApplicationCard (Rule U2)", () => {
  const activeHold: HoldSummary = {
    holdId: "hold-active-123",
    classId: "class-456",
    className: "Grade 5 - Section A",
    status: "HELD",
    expiresAt: new Date(Date.now() + 600_000).toISOString(),
  };

  it("renders active hold with time remaining and enabled action buttons", () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    const onCancel = vi.fn().mockResolvedValue(undefined);

    render(
      <ApplicationCard
        hold={activeHold}
        isProcessing={false}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );

    expect(screen.getByText("Grade 5 - Section A")).toBeInTheDocument();
    expect(screen.getByText("HELD")).toBeInTheDocument();
    expect(screen.getByRole("timer")).toBeInTheDocument();

    const payButton = screen.getByRole("button", { name: /Pay Admission Fee/i });
    const cancelButton = screen.getByRole("button", { name: /Cancel/i });

    expect(payButton).not.toBeDisabled();
    expect(cancelButton).not.toBeDisabled();

    fireEvent.click(payButton);
    expect(onConfirm).toHaveBeenCalledWith("hold-active-123");
  });

  it("shows EXPIRED and disables Pay Admission Fee when hold has expired (Rule U2)", () => {
    const expiredHold: HoldSummary = {
      ...activeHold,
      expiresAt: new Date(Date.now() - 5_000).toISOString(), // 5 seconds in the past
    };

    render(
      <ApplicationCard
        hold={expiredHold}
        isProcessing={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText("EXPIRED")).toBeInTheDocument();
    expect(
      screen.getByText(/This seat hold has expired. The seat has been released for others./i)
    ).toBeInTheDocument();

    const payButton = screen.getByRole("button", { name: /Pay Admission Fee/i });
    expect(payButton).toBeDisabled();

    const cancelButton = screen.getByRole("button", { name: /Cancel/i });
    expect(cancelButton).toBeDisabled();
  });

  it("disables buttons when an operation is in-flight (isProcessing = true)", () => {
    render(
      <ApplicationCard
        hold={activeHold}
        isProcessing={true}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    const buttons = screen.getAllByRole("button", { name: /Processing…/i });
    expect(buttons).toHaveLength(2);
    buttons.forEach((btn) => expect(btn).toBeDisabled());
  });

  it("renders CONFIRMED state with secured message and no payment buttons", () => {
    const confirmedHold: HoldSummary = {
      ...activeHold,
      status: "CONFIRMED",
    };

    render(
      <ApplicationCard
        hold={confirmedHold}
        isProcessing={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText("CONFIRMED")).toBeInTheDocument();
    expect(
      screen.getByText(/Admission confirmed. Your seat is secured!/i)
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Pay Admission Fee/i })).not.toBeInTheDocument();
  });

  it("renders CANCELLED state with appropriate message", () => {
    const cancelledHold: HoldSummary = {
      ...activeHold,
      status: "CANCELLED",
    };

    render(
      <ApplicationCard
        hold={cancelledHold}
        isProcessing={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByText("CANCELLED")).toBeInTheDocument();
    expect(
      screen.getByText(/This application was cancelled./i)
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Pay Admission Fee/i })).not.toBeInTheDocument();
  });
});
