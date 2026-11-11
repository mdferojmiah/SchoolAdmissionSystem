import { useRole } from "../context/RoleContext";

export function RoleSwitcher() {
  const {
    role,
    storageWarning,
    selectGuardian,
    selectAdmin,
  } = useRole();

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
      {storageWarning && (
        <p className="field-error" role="status">
          Role changes will last for this session only. This browser is
          blocking storage.
        </p>
      )}
    </nav>
  );
}
