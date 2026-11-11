import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, beforeEach } from "vitest";
import { RoleProvider, useRole } from "../context/RoleContext";
import { RoleSwitcher } from "./RoleSwitcher";

function RoleStatusDisplay() {
  const { role, isAdmin } = useRole();
  return (
    <div>
      <span data-testid="current-role">{role}</span>
      <span data-testid="is-admin">{isAdmin ? "yes" : "no"}</span>
    </div>
  );
}

describe("RoleSwitcher direct toggle (Rule U1, U4)", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("renders Guardian as active by default", () => {
    render(
      <RoleProvider>
        <RoleSwitcher />
        <RoleStatusDisplay />
      </RoleProvider>,
    );

    const guardianBtn = screen.getByRole("button", { name: "Guardian" });
    const adminBtn = screen.getByRole("button", { name: "Admin" });

    expect(guardianBtn).toHaveAttribute("aria-pressed", "true");
    expect(adminBtn).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByTestId("current-role")).toHaveTextContent("guardian");
    expect(screen.getByTestId("is-admin")).toHaveTextContent("no");
    expect(screen.queryByRole("button", { name: /sign out/i })).not.toBeInTheDocument();
  });

  it("switches to Admin immediately on click without credential entry", async () => {
    const user = userEvent.setup();

    render(
      <RoleProvider>
        <RoleSwitcher />
        <RoleStatusDisplay />
      </RoleProvider>,
    );

    const adminBtn = screen.getByRole("button", { name: "Admin" });
    await user.click(adminBtn);

    expect(adminBtn).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Guardian" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByTestId("current-role")).toHaveTextContent("admin");
    expect(screen.getByTestId("is-admin")).toHaveTextContent("yes");
    // Verify there is no sign-out button needed
    expect(screen.queryByRole("button", { name: /sign out/i })).not.toBeInTheDocument();
  });

  it("switches back to Guardian on click", async () => {
    const user = userEvent.setup();

    render(
      <RoleProvider>
        <RoleSwitcher />
        <RoleStatusDisplay />
      </RoleProvider>,
    );

    await user.click(screen.getByRole("button", { name: "Admin" }));
    expect(screen.getByTestId("is-admin")).toHaveTextContent("yes");

    await user.click(screen.getByRole("button", { name: "Guardian" }));
    expect(screen.getByTestId("current-role")).toHaveTextContent("guardian");
    expect(screen.getByTestId("is-admin")).toHaveTextContent("no");
  });
});
