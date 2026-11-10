# AI Usage

- Tool: opencode with the Superpowers skills.
- FER-5: AI generated the .NET solution skeleton, cross-cutting backend setup, Dockerfiles, Compose file and initial documents. Each step was verified with `dotnet test`, Docker builds or Compose runs.
- Backend tests are kept local-only by request: `backend/tests/` is git-ignored and is not part of the committed solution or GitHub repository.
- Mistakes:
  - The initial solution was created as `.slnx`; recreated it with `dotnet new sln --format sln` so the committed solution is `SchoolAdmissionSystem.sln`.
  - The template `UnitTest1.cs` remained in the local test project; removed it so only the intended tests run.
  - Compose set `ConnectionStrings__Default` on the `backend-tests` service. That ambient value made `MissingConnectionString_ThrowsInvalidOperationException` fail inside Docker even though the test cleared its configuration sources. The test now removes the ambient `ConnectionStrings__Default` and `DATABASE_URL` variables and restores them in a `finally` block, so the Compose service keeps its database connection string.
  - Serilog emitted framework `Microsoft.AspNetCore` request lines alongside the custom request line. Added a minimum-level override for `Microsoft.AspNetCore` so each request logs exactly one line.
