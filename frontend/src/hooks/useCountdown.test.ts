import { renderHook, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { useCountdown } from "./useCountdown";

describe("useCountdown hook", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("formats minutes and seconds remaining", () => {
    const now = Date.now();
    const expiresAt = new Date(now + 125_000).toISOString(); // 2m 5s

    const { result } = renderHook(() => useCountdown(expiresAt));

    expect(result.current.isExpired).toBe(false);
    expect(result.current.display).toBe("02:05");
  });

  it("transitions to expired when time runs out without page reload", () => {
    const now = Date.now();
    const expiresAt = new Date(now + 2_000).toISOString();

    const { result } = renderHook(() => useCountdown(expiresAt));
    expect(result.current.isExpired).toBe(false);

    act(() => {
      vi.advanceTimersByTime(3_000);
    });

    expect(result.current.isExpired).toBe(true);
    expect(result.current.display).toBe("00:00");
  });

  it("returns 00:00 immediately for past timestamp", () => {
    const past = new Date(Date.now() - 10_000).toISOString();

    const { result } = renderHook(() => useCountdown(past));
    expect(result.current.isExpired).toBe(true);
    expect(result.current.display).toBe("00:00");
  });
});
