import { AdminSignIn } from "./components/AdminSignIn";
import { RoleSwitcher } from "./components/RoleSwitcher";
import { StudentIdBar } from "./components/StudentIdBar";
import { ErrorBannerProvider } from "./context/ErrorBannerContext";
import { RoleProvider, useRole } from "./context/RoleContext";
import { StudentProvider } from "./context/StudentContext";

export default function App() {
  return (
    <RoleProvider>
      <StudentProvider>
        <ErrorBannerProvider>
          <AppShell />
        </ErrorBannerProvider>
      </StudentProvider>
    </RoleProvider>
  );
}

function AppShell() {
  const { role, isAdmin } = useRole();
  const isGuardian = role === "guardian";

  return (
    <div className="app">
      <header className="app-header">
        <h1>School Admission System</h1>
        <RoleSwitcher />
      </header>

      {isGuardian && <StudentIdBar />}

      <main>
        {role === "admin" && !isAdmin && <AdminSignIn />}

        {role === "admin" && isAdmin && (
          <section className="panel" aria-label="Admin">
            <h2>Admin</h2>
            <p>Create a class and see live seat counts here.</p>
          </section>
        )}

        {isGuardian && (
          <section className="panel" aria-label="Guardian">
            <h2>Guardian</h2>
            <p>Browse classes and your applications will appear here.</p>
          </section>
        )}
      </main>
    </div>
  );
}
