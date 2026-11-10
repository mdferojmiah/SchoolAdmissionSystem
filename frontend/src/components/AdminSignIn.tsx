import { useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { useRole } from "../context/RoleContext";

export function AdminSignIn() {
  const { signIn } = useRole();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  function handleEmailChange(event: ChangeEvent<HTMLInputElement>) {
    setEmail(event.target.value);
    setError(null);
  }

  function handlePasswordChange(event: ChangeEvent<HTMLInputElement>) {
    setPassword(event.target.value);
    setError(null);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (signIn(email, password)) {
      setError(null);
      return;
    }

    setError("Email or password is not correct.");
  }

  return (
    <section className="admin-sign-in" aria-label="Admin sign in">
      <h2>Admin sign in</h2>
      <form onSubmit={handleSubmit} noValidate>
        <label htmlFor="admin-email">Email</label>
        <input
          id="admin-email"
          name="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={handleEmailChange}
        />
        <label htmlFor="admin-password">Password</label>
        <input
          id="admin-password"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={handlePasswordChange}
        />
        <button type="submit">Sign in</button>
        {error && (
          <p className="field-error" role="alert">
            {error}
          </p>
        )}
      </form>
    </section>
  );
}
