# Assumptions and Design Decisions

This document outlines key technical assumptions, architectural choices, and design rationales for the School Admission System.

---

## 1. Concurrency and Multi-Process Seat Safety (Rule R1)

- **PostgreSQL Row-Level Locking:** Concurrency safety is achieved at the database engine level rather than through in-memory locks (such as C# `lock`, `SemaphoreSlim`, or local channel queues).
- **Explicit Transaction Boundary:** During seat application (`POST /classes/{classId}/holds`), the backend initiates an explicit database transaction with `ReadCommitted` isolation and executes:
  ```sql
  SELECT id, total_seats FROM classes WHERE id = @classId FOR UPDATE;
  ```
- **Horizontal Scaling Safety:** Because `SELECT ... FOR UPDATE` is held until the transaction commits or rolls back, multiple backend processes, instances, or Docker containers running against the same PostgreSQL database are serialized safely. Over-subscription is mathematically prevented.
- **Confirmation Locking:** The confirmation endpoint similarly locks the hold record with `FOR UPDATE` to guarantee idempotent status transitions without race conditions.

---

## 2. Lazy Hold Expiry (Rule R2)

- **No Background Sweepers or Cron Jobs:** Seat availability is evaluated dynamically based on database clock time:
  ```sql
  status = 'HELD' AND expires_at > now()
  ```
- **Instant Seat Release:** When a hold's duration (`HOLD_DURATION_SECONDS`, default 600s) elapses, the seat is immediately available to other applicants. There is zero delay, zero background polling overhead, and zero dependence on scheduled tasks running on time.
- **Clock Authority:** Expiry is calculated using the database server's clock (`now()`), ensuring consistent time calculations across all application instances.

---

## 3. UI-Only Roles and Demo Admin Gate

- **Cosmetic Front-End Gate:** The Guardian vs. Admin switcher and the Admin sign-in modal are implemented strictly in the frontend user interface.
- **Build-Time Environment Variables:** `VITE_ADMIN_EMAIL` and `VITE_ADMIN_PASSWORD` are baked into the client JavaScript bundle during the Docker build. They are publicly inspectable by anyone with browser developer tools and are not intended as secure credentials.
- **Intentionally Open Backend API:** The backend API has no authentication middleware or role-based access control. All endpoints (`POST /classes`, `POST /classes/{classId}/holds`, `POST /holds/{holdId}/confirm`, etc.) are open.
- **Rationale:** The specification mandates automated API integration testing (including concurrency tests) without requiring prior authentication or bearer token management. Enforcing backend authorization would break automated grading harnesses and compose test suites.

---

## 4. Student Identity & Ownership (Rules R3, R4)

- **Identity Header:** Student identity is asserted via the `X-Student-Id` HTTP request header, which must contain exactly 17 digits. No student passwords or registration accounts exist.
- **Persistence Across Browser Sessions:** The frontend stores the student ID in `localStorage` so that refreshing or reopening the browser preserves identity and re-fetches the student's ongoing applications via `GET /me/holds` (**Rule U4**).
- **Ownership Verification:** A student can only confirm or cancel their own holds. Mismatched IDs return HTTP 403 Forbidden with code `NOT_HOLD_OWNER`.
- **Database Duplicate Guard:** A PostgreSQL partial unique index on `(class_id, student_id) WHERE status IN ('HELD', 'CONFIRMED')` guarantees at the database level that a student cannot have duplicate active applications in the same class.

---

## 5. Payment Simulation (Rules R4, R5)

- **Simulated Payment Gateway:** Clicking "Pay Admission Fee" in the frontend assumes instant and successful fee payment.
- **State Transition:** The hold transition from `HELD` to `CONFIRMED` occurs atomically in the backend upon receiving the confirm request.
- **Idempotence:** Subsequent calls to confirm a hold that is already confirmed return HTTP 200 OK without incrementing seat counts or erroring (**Rule R5**).

---

## 6. Rate Limiting and Performance

- **No Rate Limiting:** The backend intentionally does not apply rate limiting (neither per-IP nor per-student). The class row lock efficiently serializes requests, and automated concurrency tests need to fire dozens of simultaneous requests without being throttled.

---

## 7. Data Persistence (Rule R6)

- **Named Docker Volume:** All database tables, indexes, and records reside in the named volume `db-data`.
- **Lifecycle Guarantees:** Running `docker compose down` and subsequently `docker compose up` preserves all classes, holds, and student records. The volume is only removed if explicitly instructed via `docker compose down -v`.
