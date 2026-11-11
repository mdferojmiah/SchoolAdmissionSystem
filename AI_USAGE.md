# AI Usage and Methodology

This document details how AI assistance was leveraged during the development of the School Admission System, the engineering principles and guardrails I enforced, and how I actively directed the AI to produce a robust, production-grade system.

---

## 1. Engineering Leadership & Development Philosophy

Rather than treating the AI as an autonomous developer, I approached the project with a strict **human-in-the-loop pair programming model**, acting as the software architect and technical reviewer while directing the AI as an implementation assistant.

### Core Working Principles
1. **Human-Led Architecture:** I established the foundational architecture, domain model boundaries, and database locking strategies before any code was written. The AI was not allowed to improvise architectural patterns or introduce unauthorized third-party dependencies.
2. **Spec-Driven Incremental Slices:** I decomposed the project into discrete, verifiable development slices. For each slice, I provided explicit scope, expected file changes, exact acceptance criteria, and verification commands.
3. **Rigorous Test-Driven Discipline (TDD):** I enforced a test-first workflow. For each functional requirement, I instructed the AI to write failing unit or integration tests, run them inside containerized test environments to observe the failure, write the minimal production code to satisfy the test, and verify the green state before committing.
4. **Frequent Small Commits & Quality Gates:** Code was committed in granular increments only after automated verification passed. At each step, I reviewed the generated code for standards compliance, error handling, and performance implications.

---

## 2. Key Guardrails & Constraints Enforced on the AI

To prevent common AI pitfalls (hallucinated packages, fragile in-memory hacks, broken concurrency models, or excessive framework bloat), I enforced non-negotiable technical constraints from day one:

- **Database-Level Row Locking over In-Memory Synchronization:**
  A common failure mode for AI models is proposing local concurrency primitives (such as C# `lock`, `SemaphoreSlim`, or memory queues). I explicitly mandated that seat safety must be enforced at the database engine level via PostgreSQL `SELECT ... FOR UPDATE` inside an explicit transaction boundary. This guaranteed true horizontal scalability and safety across multiple processes.
- **Dynamic (Lazy) Expiry without Background Jobs:**
  I directed the AI away from heavy background sweepers, Quartz jobs, or periodic cron tasks for hold expirations. Instead, I required seat availability to be evaluated dynamically using the database clock (`status = 'HELD' AND expires_at > now()`). This made seat reclamation instantaneous and eliminated distributed timer race conditions.
- **PostgreSQL Partial Unique Indexes for Duplicate Prevention:**
  I required that student duplicate prevention be backed by a database-level partial unique index (`class_id, student_id WHERE status IN ('HELD', 'CONFIRMED')`). When Postgres returns unique violation error `23505`, the backend translates it cleanly to HTTP 409 Conflict rather than uncaught 500 errors.
- **Zero-Dependency Docker Testing:**
  I prohibited Testcontainers or external cloud dependencies for backend testing. I mandated that all automated integration tests run directly against the project's PostgreSQL container via Docker Compose (`docker compose run --rm backend-tests`).
- **Standardized Application Architecture & Contracts:**
  Inside `SchoolAdmission.Application`, I enforced a predictable three-folder structure per entity (`Services/`, `Dtos/`, `Validators/`) with stable namespaces. For error reporting, I mandated strict RFC 7807 ProblemDetails with uniform `code` and `message` properties.
- **Privacy-First Audit Logging:**
  I instructed the AI to configure Serilog to output exactly one line per request, capturing method, route, HTTP status, and duration, while masking the 17-digit student ID to its last 4 digits (`student ****4567`) and never logging sensitive request bodies.

---

## 3. Human Oversight, Debugging & Course Corrections

During development, my code reviews and runtime inspections caught several subtle bugs, framework incompatibilities, and edge cases where I redirected the AI:

1. **Client-Server Clock Skew in Hold Countdown:**
   - *Observation:* A client machine with a clock running fast or slow relative to the server would miscalculate the 10-minute hold countdown, showing an expired status prematurely or displaying inaccurate seconds.
   - *Direction:* I instructed the AI to capture the HTTP response `Date` header via the API client, compute the clock offset against the local `Date.now()`, and synchronize the React countdown timer hook to the server's authoritative clock.
2. **Framework Request Log Duplication in ASP.NET Core:**
   - *Observation:* Default ASP.NET Core hosting diagnostics emitted redundant request log entries alongside our custom Serilog audit logs.
   - *Direction:* I directed the AI to set a minimum log level override for `Microsoft.AspNetCore` (`LogEventLevel.Warning`) in the host builder, restoring clean, single-line audit logging.
3. **Ambient Environment Variable Leakage in Test Containers:**
   - *Observation:* In Docker Compose, the `backend-tests` container had `ConnectionStrings__Default` populated in its environment. A unit test checking that a missing connection string throws an exception picked up the ambient variable and unexpectedly failed.
   - *Direction:* I guided the AI to isolate the test fixture using a defensive `try ... finally` block that temporarily clears and restores ambient connection strings during the test execution.
4. **Defensive Browser Storage Handling:**
   - *Observation:* Browsers running in private/incognito mode or with strict privacy configurations throw `DOMException` when attempting to write to `localStorage`.
   - *Direction:* I required the frontend storage utility to wrap all `localStorage` access in defensive try/catch blocks with graceful in-memory fallbacks, preventing blank screens or unhandled exceptions for incognito users.
5. **Multi-Process Concurrency Verification Design:**
   - *Observation:* Unit tests inside a single process cannot empirically prove that multiple horizontally scaled backend web hosts will not over-allocate seats.
   - *Direction:* I architected a multi-process test harness (`MultiProcessConcurrencyTests.cs`) that spawns two distinct Kestrel instances on separate ports (`5101` and `5102`) against the same PostgreSQL database, firing dozens of concurrent seat requests across both servers to prove zero over-subscription.

---

## 4. Functional Areas Assisted by AI

The AI assisted in translating high-level architectural designs and specifications into modular, tested code across the following functional areas:

- **Repository & Infrastructure Setup:**
  Generation of multi-stage Dockerfiles (.NET 10 SDK, Node 22 with Nginx), `docker-compose.yml`, solution configuration, global CORS policy, Serilog request logging, and RFC 7807 global exception handling.
- **Database Schema & Migrations:**
  Entity Framework Core 10 entity definitions, PostgreSQL partial unique indexes, migration scripting, and automatic database migration execution on application startup.
- **Class Management API & Validation:**
  Class creation, listing, real-time seat availability calculation (`totalSeats`, `heldSeats`, `confirmedSeats`, `availableSeats`), DTO records, and fluent input validators.
- **Concurrency-Safe Seat Hold Engine:**
  Implementation of the seat hold transaction with PostgreSQL `SELECT ... FOR UPDATE` row locking, seat limit validation, duplicate student checks, and 17-digit student ID format enforcement.
- **Hold Lifecycle & Confirmation:**
  Endpoints for inspecting hold status, idempotent payment confirmation, manual hold cancellation, and student hold queries (`GET /me/holds`) with ownership validation (`X-Student-Id`).
- **Frontend Architecture & Reactive UI:**
  React 19 + TypeScript + Vite single-page application featuring Guardian/Admin role toggling, persistent student ID storage, auto-refreshing class lists, server-skew-corrected countdown timers, instant zero-reload expiration handling, and dismissible error banners.
- **Automated Test Suites:**
  Full suite of backend integration tests verifying seat guarantees under high concurrency, duplicate rejections, confirmation idempotence, dynamic expiry, and applicant ownership, alongside Vitest component tests for the frontend UI.

---

## 5. Reflections on AI Collaboration

- **Where AI Accelerated Progress:**
  The AI was remarkably effective at generating repetitive boilerplate, scaffolding typed TypeScript interfaces matching backend DTOs, building out comprehensive test scenarios, and ensuring type safety across strict compiler options.
- **Where Human Engineering was Indispensable:**
  The AI lacked inherent intuition for distributed systems guarantees, transaction isolation levels, and edge cases involving multi-container environments. Without upfront human constraints (such as mandating database-level row locks and lazy expiry), the AI would have naturally defaulted to naive in-memory locks and fragile background timers that fail in production.
- **Conclusion:**
  Pairing clear engineering specifications and strict architectural boundaries with an AI coding assistant resulted in clean, tested, and robust software delivered with exceptional velocity while maintaining complete architectural integrity.
