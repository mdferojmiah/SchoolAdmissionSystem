# Assumptions

- PostgreSQL data is stored in the named Docker volume `db-data`. `docker compose down` keeps it; `docker compose down -v` deliberately removes it (R6).
- The backend test project is local-only by request. `backend/tests/` is git-ignored and excluded from the committed solution, so a fresh clone can run `docker compose up` but does not contain the tests.
- Roles and the demo admin gate are UI-only and cosmetic. Final details are added when the frontend features are implemented.
- No rate limiting is used. The class row lock protects seat correctness; automated tests send many parallel requests from one client.
