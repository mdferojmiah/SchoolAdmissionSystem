import { useRole } from "../context/RoleContext";

export function RoleSwitcher() {
  const { role, adminSignedIn, selectGuardian, selectAdmin, signOut } =
    useRole();

  return (
    <nav className="role-switcher" aria-label="Role">
      <button
        type="button"
        aria-pressed={role === "guardian"}
        onClick={selectGuardian}
      >
        Guardian
      </button>
      <button
        type="button"
        aria-pressed={role === "admin"}
        onClick={selectAdmin}
      >
        Admin
      </button>
      {role === "admin" && adminSignedIn && (
        <button type="button" className="sign-out" onClick={signOut}>
          Sign out
        </button>
      )}
    </nav>
  );
}
