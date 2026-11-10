import { AdminSignIn } from "./components/AdminSignIn";
import { RoleSwitcher } from "./components/RoleSwitcher";
import { StudentIdBar } from "./components/StudentIdBar";
import { ErrorBannerProvider } from "./context/ErrorBannerContext";
import { RoleProvider, useRole } from "./context/RoleContext";
import { StudentProvider } from "./context/StudentContext";
import { ClassesPage } from "./pages/ClassesPage";

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

        {(isGuardian || isAdmin) && <ClassesPage />}
      </main>
    </div>
  );
}
