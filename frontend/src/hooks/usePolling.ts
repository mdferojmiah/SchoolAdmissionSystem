import { useCallback, useEffect, useRef, useState } from "react";

export const DEFAULT_POLL_INTERVAL_MS = 4000;

export interface PollingState<T> {
  data: T | null;
  error: unknown;
  isLoading: boolean;
  refresh: () => Promise<void>;
}

export function usePolling<T>(
  fetcher: () => Promise<T>,
  intervalMs: number = DEFAULT_POLL_INTERVAL_MS,
): PollingState<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [isLoading, setIsLoading] = useState(true);

  const fetcherRef = useRef(fetcher);
  const mountedRef = useRef(true);
  const inFlightRef = useRef<Promise<void> | null>(null);

  useEffect(() => {
    fetcherRef.current = fetcher;
  }, [fetcher]);

  const run = useCallback(async () => {
    if (inFlightRef.current) {
      return inFlightRef.current;
    }

    const task = (async () => {
      try {
        const next = await fetcherRef.current();
        if (mountedRef.current) {
          setData(next);
          setError(null);
        }
      } catch (caught) {
        if (mountedRef.current) {
          setError(caught);
        }
      } finally {
        if (mountedRef.current) {
          setIsLoading(false);
        }
      }
    })();

    inFlightRef.current = task;

    try {
      await task;
    } finally {
      inFlightRef.current = null;
    }
  }, []);

  const refresh = useCallback(async () => {
    if (inFlightRef.current) {
      await inFlightRef.current;
    }

    await run();
  }, [run]);

  useEffect(() => {
    mountedRef.current = true;
    void run();

    const intervalId = window.setInterval(() => {
      void run();
    }, intervalMs);

    return () => {
      mountedRef.current = false;
      window.clearInterval(intervalId);
    };
  }, [intervalMs, run]);

  return { data, error, isLoading, refresh };
}
