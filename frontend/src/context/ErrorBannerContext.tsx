import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";
import { ErrorBanner } from "../components/ErrorBanner";

export interface ErrorBannerValue {
  showError(message: string): void;
  clearError(): void;
}

const ErrorBannerContext = createContext<ErrorBannerValue | null>(null);

export function ErrorBannerProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState<string | null>(null);

  const showError = useCallback((next: string) => {
    setMessage(next);
  }, []);

  const clearError = useCallback(() => {
    setMessage(null);
  }, []);

  const value = useMemo<ErrorBannerValue>(
    () => ({ showError, clearError }),
    [showError, clearError],
  );

  return (
    <ErrorBannerContext.Provider value={value}>
      {message && <ErrorBanner message={message} onDismiss={clearError} />}
      {children}
    </ErrorBannerContext.Provider>
  );
}

export function useErrorBanner(): ErrorBannerValue {
  const context = useContext(ErrorBannerContext);
  if (!context) {
    throw new Error("useErrorBanner must be used inside ErrorBannerProvider.");
  }

  return context;
}
