# School Admission System

First-come seat holds with timed expiry and pay-to-confirm admission.

## Run

```bash
docker compose up --build
```

- Frontend: http://localhost:3000
- Backend: http://localhost:8080
- PostgreSQL runs as the Compose service `db` and stores data in the named volume `db-data`.

The hold duration is `HOLD_DURATION_SECONDS` (default `600`).

## Demo admin

- Email: `admin@school.test`
- Password: `Adm1n!Demo`

## Backend Automated Tests

All automated tests run against the PostgreSQL 16 database container via Docker Compose. No local .NET SDK or Testcontainers configuration is required.

Run the entire automated test suite:

```bash
docker compose run --rm backend-tests
```

To run specifically the 5 core specification integration tests (`AdmissionSystemIntegrationTests`):

```bash
docker compose run --rm backend-tests --filter FullyQualifiedName~AdmissionSystemIntegrationTests
```

### Specification Test Coverage (Rules R1 – R5)

The integration test suite (`SchoolAdmission.Tests/Api/AdmissionSystemIntegrationTests.cs`) covers the mandatory specification criteria:

1. **Concurrency (`SpecTest1_Concurrency_ManyParallelApplies_NeverExceedTotalSeats`):** 25 concurrent requests compete for 5 seats; exactly 5 succeed (201 Created), 20 receive 409 Conflict with code `NO_SEATS`, and held + confirmed seats never exceed 5 (**Rule R1**).
2. **Duplicate Application (`SpecTest2_DuplicateApply_SameStudentApplyingTwice_IsRejectedWithConflict`):** Applying twice to the same class while active or confirmed returns 409 Conflict with code `DUPLICATE_APPLICATION` (**Rule R3**).
3. **Confirm Idempotence (`SpecTest3_ConfirmTwice_RaisesConfirmedSeatsOnlyOnce`):** Calling confirm repeatedly or concurrently returns 200 OK and increments `confirmedSeats` only once (**Rule R5**).
4. **Hold Expiry (`SpecTest4_ExpiredHold_FreesSeatAndCannotBeConfirmed`):** Expired holds immediately free their seat without background jobs, and confirm requests on expired holds return 409 `HOLD_EXPIRED` (**Rules R2, R4**).
5. **Ownership (`SpecTest5_Ownership_StudentCannotConfirmOrCancelAnotherStudentsHold`):** Non-owners attempting to confirm or cancel receive 403 Forbidden with code `NOT_HOLD_OWNER` (**Rule R4**).

## Persistence (R6)

PostgreSQL data lives in the named volume `db-data`. `docker compose down` followed by `docker compose up` keeps all classes and holds. `docker compose down -v` removes the volume and all data.
