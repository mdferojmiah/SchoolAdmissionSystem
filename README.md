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

## Backend tests (local working copy only)

The `backend/tests/` project is intentionally not committed. In a working copy that contains it, run:

```bash
docker compose run --rm backend-tests
```

A fresh clone does not include tests; `docker compose up` still runs the application.

## Persistence (R6)

PostgreSQL data lives in the named volume `db-data`. `docker compose down` followed by `docker compose up` keeps all classes and holds. `docker compose down -v` removes the volume and all data.
