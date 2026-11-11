# School Admission System

A concurrency-safe school admission seat hold system featuring first-come seat reservations, timed hold expiry, and pay-to-confirm admission. Built with **.NET 10**, **PostgreSQL 16**, **React 19.3.1**, and **Docker Compose**.

---

## Quickstart

Start the entire system (database, backend API, and frontend web application) with a single command:

```bash
docker compose up --build
```

### Service URLs

- **Frontend Application:** [http://localhost:3000](http://localhost:3000)
- **Backend API:** [http://localhost:8080](http://localhost:8080)
- **Health Check:** [http://localhost:8080/health](http://localhost:8080/health)

PostgreSQL runs as the `db` service and stores all data in the persistent named volume `db-data`. The hold duration is configurable via `HOLD_DURATION_SECONDS` (default: `600` seconds / 10 minutes).

---

## Roles and UI Navigation

The frontend application provides a role switcher in the top navigation bar:

### 1. Guardian Mode (Default)
- **Access:** Available immediately without logging in.
- **Student ID Bar:** Enter a 17-digit student identification number (e.g., `12345678901234567`). The ID is validated and saved to `localStorage` so it persists across page reloads and browser restarts (**Rule U4**).
- **Class List:** View all available admission classes with live seat availability (`Total`, `Held`, `Confirmed`, and `Available`) updating every 4 seconds (**Rule U3**). Click **"Apply"** to acquire a seat hold (**Rule U1**).
- **Application Card & Countdown:** Shows ongoing applications with class name, status badge (`HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`), and a live countdown timer adjusted for server clock skew.
- **Pay to Confirm:** Click **"Pay Admission Fee"** to confirm the seat. If the hold expires (`00:00`), the status immediately updates to `EXPIRED` without a page reload and the pay button is disabled (**Rule U2**).
- **Cancel:** Click **"Cancel"** to release the hold early and immediately free the seat for other applicants.

### 2. Admin Mode
- **Access:** Click the **"Admin"** pill in the top header. Switches immediately to Admin mode without requiring credentials.
- **Class Creation:** Fill in the class name and seat capacity to create new admission classes (**Rule U1**). The newly created class immediately appears in the class list.
- **Switch Back:** Click **"Guardian"** at any time to return to the Guardian view.

> **Note on Roles:** Role switching is a lightweight UI-level toggle for demonstration purposes. The backend API remains open without authentication so automated grading suites and API test harnesses can run seamlessly.

---

## Backend Automated Tests

All tests run in containerized isolation against the PostgreSQL 16 database container via Docker Compose. No local .NET SDK or Testcontainers configuration is needed on the host machine.

Run the entire automated test suite:

```bash
docker compose run --rm backend-tests
```

To run specifically the 5 core specification integration tests (`AdmissionSystemIntegrationTests`):

```bash
docker compose run --rm backend-tests --filter FullyQualifiedName~AdmissionSystemIntegrationTests
```

To run the multi-process concurrency proof test (2 servers on ports 5101 and 5102):

```bash
docker compose run --rm backend-tests --filter FullyQualifiedName~MultiProcessConcurrencyTests
```

### Frontend Automated Tests (Vitest + React Testing Library)

Automated frontend unit and component tests verify the countdown timer expiry without page reload (**Rule U2**), disabled payment and cancel buttons, friendly error banner presentation (**Rule U5**), and crash fallback boundaries:

```bash
cd frontend
npm test
```

### Specification Test Coverage (Rules R1 – R5)

The integration test suite (`SchoolAdmission.Tests/Api/AdmissionSystemIntegrationTests.cs`) verifies the mandatory system guarantees:

| Rule | Description | Test Name |
|---|---|---|
| **R1** | Held + confirmed seats never exceed `totalSeats`, even under high concurrency. | `SpecTest1_Concurrency_ManyParallelApplies_NeverExceedTotalSeats` (25 concurrent applies for 5 seats: exactly 5 succeed, 20 receive 409 Conflict with code `NO_SEATS`). |
| **R3** | A student cannot hold more than one active or confirmed seat in the same class. | `SpecTest2_DuplicateApply_SameStudentApplyingTwice_IsRejectedWithConflict` (duplicate returns 409 `DUPLICATE_APPLICATION`). |
| **R5** | Repeated confirm requests raise `confirmedSeats` only once. | `SpecTest3_ConfirmTwice_RaisesConfirmedSeatsOnlyOnce` (subsequent confirms return 200 OK idempotently). |
| **R2, R4** | Expired holds instantly free seats without background jobs and reject payment. | `SpecTest4_ExpiredHold_FreesSeatAndCannotBeConfirmed` (expired hold returns 409 `HOLD_EXPIRED`). |
| **R4** | Only the original applicant can confirm or cancel their hold. | `SpecTest5_Ownership_StudentCannotConfirmOrCancelAnotherStudentsHold` (mismatched `X-Student-Id` returns 403 `NOT_HOLD_OWNER`). |

---

## Architecture & Concurrency Guarantees

- **Multi-Process Concurrency Safety (Rule R1):** When a seat hold is requested (`POST /classes/{classId}/holds`), the backend initiates an explicit database transaction and executes `SELECT ... FOR UPDATE` on the class record in PostgreSQL. Seat counts (`HELD` within unexpired window + `CONFIRMED`) are verified against `total_seats` before inserting the hold. Because the lock is held at the database engine level, race conditions are prevented even across multiple horizontally scaled backend instances.
- **Lazy Seat Expiry (Rule R2):** Hold duration is governed by `HOLD_DURATION_SECONDS` (default: 600 seconds). Holds are considered active only while `status = 'HELD' AND expires_at > now()`. Expired holds immediately free seats without requiring background pollers or cron jobs.
- **Database-Level Duplicate Prevention (Rule R3):** Backed by a PostgreSQL partial unique index on `(class_id, student_id) WHERE status IN ('HELD', 'CONFIRMED')`. Duplicate attempts catch Postgres error `23505` and map cleanly to HTTP 409 Conflict.
- **Structured Error Responses:** All API errors adhere to RFC 7807 ProblemDetails with machine-readable `code` and human-readable `message` properties.
- **Audit Logging:** Configured via Serilog to produce exactly one concise log line per HTTP request, recording method, path, HTTP status code, response time in milliseconds, and the student ID masked to its last 4 digits (e.g. `student ****4567`). Request bodies are never logged.

---

## Data Persistence (Rule R6)

PostgreSQL data is stored in the Docker named volume `db-data`.

- **Preserve data across restarts:**
  ```bash
  docker compose down
  docker compose up
  ```
  All classes, active holds, confirmed admissions, and timestamps remain preserved.
- **Reset data for a fresh run:**
  ```bash
  docker compose down -v
  ```
