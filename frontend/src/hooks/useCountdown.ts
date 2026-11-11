import { useEffect, useState } from "react";
import { getLastServerDate } from "../api/client";

export interface CountdownResult {
  remainingMs: number;
  display: string;
  isExpired: boolean;
}

function formatDisplay(remainingMs: number): string {
  if (remainingMs <= 0) {
    return "00:00";
  }

  const totalSeconds = Math.floor(remainingMs / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  const mm = String(minutes).padStart(2, "0");
  const ss = String(seconds).padStart(2, "0");

  if (hours > 0) {
    return `${hours}:${mm}:${ss}`;
  }

  return `${mm}:${ss}`;
}

export function useCountdown(expiresAtUtcIso: string): CountdownResult {
  const [remainingMs, setRemainingMs] = useState<number>(() => {
    const targetMs = new Date(expiresAtUtcIso).getTime();
    if (Number.isNaN(targetMs)) {
      return 0;
    }

    const serverDate = getLastServerDate();
    const offsetMs = serverDate ? serverDate.getTime() - Date.now() : 0;
    const effectiveNow = Date.now() + offsetMs;
    return Math.max(0, targetMs - effectiveNow);
  });

  useEffect(() => {
    const targetMs = new Date(expiresAtUtcIso).getTime();
    if (Number.isNaN(targetMs)) {
      setRemainingMs(0);
      return;
    }

    const calculateRemaining = () => {
      const serverDate = getLastServerDate();
      const offsetMs = serverDate ? serverDate.getTime() - Date.now() : 0;
      const effectiveNow = Date.now() + offsetMs;
      return Math.max(0, targetMs - effectiveNow);
    };

    const initial = calculateRemaining();
    setRemainingMs(initial);

    if (initial <= 0) {
      return;
    }

    const intervalId = window.setInterval(() => {
      const current = calculateRemaining();
      setRemainingMs(current);
      if (current <= 0) {
        window.clearInterval(intervalId);
      }
    }, 1000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [expiresAtUtcIso]);

  const isExpired = remainingMs <= 0;

  return {
    remainingMs,
    display: formatDisplay(remainingMs),
    isExpired,
  };
}
