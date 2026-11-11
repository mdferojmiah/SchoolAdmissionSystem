using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Api;

/// <summary>
/// Mandatory backend automated integration tests required by the project specification.
/// Tests execute against a real PostgreSQL 16 database managed by Docker Compose.
/// </summary>
[Collection("PostgresSchema")]
public sealed class AdmissionSystemIntegrationTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly List<string> _createdClassIds = [];

    public AdmissionSystemIntegrationTests(PostgresSchemaFixture fixture)
    {
        _fixture = fixture;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("ConnectionStrings:Default", fixture.ConnectionString);
            builder.UseSetting("HOLD_DURATION_SECONDS", "600");
        });
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Spec Test 1:
    /// Concurrency: Fire N parallel requests to hold seats for a class with M seats (N >> M).
    /// Assert that exactly M succeed (201 Created), N - M receive 409 Conflict,
    /// and held + confirmed seats never exceed totalSeats.
    /// </summary>
    [Fact]
    public async Task SpecTest1_Concurrency_ManyParallelApplies_NeverExceedTotalSeats()
    {
        const int totalSeats = 5;
        const int totalAttempts = 25;
        var classId = await CreateClassAsync("Spec Concurrency Class", totalSeats);

        // Fire 25 simultaneous apply requests with distinct 17-digit student IDs
        var tasks = Enumerable.Range(0, totalAttempts)
            .Select(index => ApplyAsync(classId, StudentIdAt(index)))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        try
        {
            var statuses = responses.Select(r => r.StatusCode).ToList();

            var successfulApplies = statuses.Count(s => s == HttpStatusCode.Created);
            var rejectedApplies = statuses.Count(s => s == HttpStatusCode.Conflict);

            Assert.Equal(totalSeats, successfulApplies);
            Assert.Equal(totalAttempts - totalSeats, rejectedApplies);
            Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);

            // Verify rejected responses have ProblemDetails with code "NO_SEATS"
            var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict);
            foreach (var conflictResponse in conflictResponses)
            {
                using var doc = JsonDocument.Parse(await conflictResponse.Content.ReadAsStringAsync());
                Assert.Equal("NO_SEATS", doc.RootElement.GetProperty("code").GetString());
                Assert.Equal("No seats are available in this class.", doc.RootElement.GetProperty("message").GetString());
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        // Verify class seat calculation via GET /classes/{classId}
        using var classDoc = await GetClassAsync(classId);
        Assert.Equal(totalSeats, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, classDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, classDoc.RootElement.GetProperty("availableSeats").GetInt32());

        // Verify active holds directly in the database
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM holds WHERE class_id = @classId AND status = 'HELD' AND expires_at > now()",
            connection);
        cmd.Parameters.AddWithValue("classId", classId);

        var activeHolds = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal((long)totalSeats, activeHolds);
    }

    /// <summary>
    /// Spec Test 2:
    /// Duplicate apply: Verify that the same student applying twice to the same class
    /// is rejected (409 Conflict) while active or confirmed.
    /// </summary>
    [Fact]
    public async Task SpecTest2_DuplicateApply_SameStudentApplyingTwice_IsRejectedWithConflict()
    {
        var classId = await CreateClassAsync("Spec Duplicate Apply Class", 10);
        var studentId = NewStudentId();

        // 1. First apply succeeds (201 Created)
        using (var firstResponse = await ApplyAsync(classId, studentId))
        {
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
            using var doc = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
            Assert.Equal("HELD", doc.RootElement.GetProperty("status").GetString());
        }

        // 2. Second apply while hold is HELD is rejected (409 Conflict)
        using (var secondResponse = await ApplyAsync(classId, studentId))
        {
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
            using var doc = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
            Assert.Equal("DUPLICATE_APPLICATION", doc.RootElement.GetProperty("code").GetString());
            Assert.Equal("You have already applied to this class.", doc.RootElement.GetProperty("message").GetString());
            Assert.False(doc.RootElement.TryGetProperty("stackTrace", out _));
        }

        // 3. Confirm the existing hold
        var holdId = await GetActiveHoldIdAsync(classId, studentId);
        using (var confirmResponse = await ConfirmAsync(holdId, studentId))
        {
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        }

        // 4. Third apply after admission is CONFIRMED is also rejected (409 Conflict)
        using (var thirdResponse = await ApplyAsync(classId, studentId))
        {
            Assert.Equal(HttpStatusCode.Conflict, thirdResponse.StatusCode);
            using var doc = JsonDocument.Parse(await thirdResponse.Content.ReadAsStringAsync());
            Assert.Equal("DUPLICATE_APPLICATION", doc.RootElement.GetProperty("code").GetString());
        }

        // Verify class still has only 1 confirmed seat and 0 held seats
        using var classDoc = await GetClassAsync(classId);
        Assert.Equal(1, classDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(9, classDoc.RootElement.GetProperty("availableSeats").GetInt32());
    }

    /// <summary>
    /// Spec Test 3:
    /// Confirm twice: Verify that confirming a hold twice returns the hold
    /// but raises confirmedSeats only once. Concurrent confirms on the same hold
    /// also increment confirmedSeats only once.
    /// </summary>
    [Fact]
    public async Task SpecTest3_ConfirmTwice_RaisesConfirmedSeatsOnlyOnce()
    {
        var classId = await CreateClassAsync("Spec Confirm Idempotence Class", 5);
        var studentId = NewStudentId();

        // 1. Apply to get hold
        string holdId;
        using (var applyResponse = await ApplyAsync(classId, studentId))
        {
            Assert.Equal(HttpStatusCode.Created, applyResponse.StatusCode);
            using var doc = JsonDocument.Parse(await applyResponse.Content.ReadAsStringAsync());
            holdId = doc.RootElement.GetProperty("holdId").GetString()!;
        }

        // 2. First confirm -> 200 OK, status CONFIRMED
        using (var firstConfirm = await ConfirmAsync(holdId, studentId))
        {
            Assert.Equal(HttpStatusCode.OK, firstConfirm.StatusCode);
            using var doc = JsonDocument.Parse(await firstConfirm.Content.ReadAsStringAsync());
            Assert.Equal("CONFIRMED", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(holdId, doc.RootElement.GetProperty("holdId").GetString());
        }

        // Verify class confirmedSeats is 1
        using (var classDoc1 = await GetClassAsync(classId))
        {
            Assert.Equal(1, classDoc1.RootElement.GetProperty("confirmedSeats").GetInt32());
            Assert.Equal(0, classDoc1.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(4, classDoc1.RootElement.GetProperty("availableSeats").GetInt32());
        }

        // 3. Second confirm -> 200 OK (idempotent), status still CONFIRMED
        using (var secondConfirm = await ConfirmAsync(holdId, studentId))
        {
            Assert.Equal(HttpStatusCode.OK, secondConfirm.StatusCode);
            using var doc = JsonDocument.Parse(await secondConfirm.Content.ReadAsStringAsync());
            Assert.Equal("CONFIRMED", doc.RootElement.GetProperty("status").GetString());
            Assert.Equal(holdId, doc.RootElement.GetProperty("holdId").GetString());
        }

        // Verify class confirmedSeats remained exactly 1
        using (var classDoc2 = await GetClassAsync(classId))
        {
            Assert.Equal(1, classDoc2.RootElement.GetProperty("confirmedSeats").GetInt32());
            Assert.Equal(0, classDoc2.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(4, classDoc2.RootElement.GetProperty("availableSeats").GetInt32());
        }

        // 4. Concurrent confirms test: create another hold and fire 10 simultaneous confirms
        var concurrentStudentId = NewStudentId();
        string concurrentHoldId;
        using (var applyResponse = await ApplyAsync(classId, concurrentStudentId))
        {
            using var doc = JsonDocument.Parse(await applyResponse.Content.ReadAsStringAsync());
            concurrentHoldId = doc.RootElement.GetProperty("holdId").GetString()!;
        }

        var confirmTasks = Enumerable.Range(0, 10)
            .Select(_ => ConfirmAsync(concurrentHoldId, concurrentStudentId))
            .ToList();

        var confirmResponses = await Task.WhenAll(confirmTasks);
        try
        {
            Assert.All(confirmResponses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        }
        finally
        {
            foreach (var r in confirmResponses) r.Dispose();
        }

        // Total confirmed seats in class must now be exactly 2 (first student + second student)
        using (var classDoc3 = await GetClassAsync(classId))
        {
            Assert.Equal(2, classDoc3.RootElement.GetProperty("confirmedSeats").GetInt32());
            Assert.Equal(0, classDoc3.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(3, classDoc3.RootElement.GetProperty("availableSeats").GetInt32());
        }
    }

    /// <summary>
    /// Spec Test 4:
    /// Expired hold: Verify that an expired hold frees the seat (without an API call
    /// or background worker) and cannot be confirmed (409 Conflict).
    /// </summary>
    [Fact]
    public async Task SpecTest4_ExpiredHold_FreesSeatAndCannotBeConfirmed()
    {
        // 1. Create a class with exactly 1 total seat
        var classId = await CreateClassAsync("Spec Expiry Class", 1);
        var student1 = NewStudentId();

        // 2. Insert an expired hold directly with expires_at in the past (10 minutes ago)
        var expiredHoldId = $"h_exp_{Guid.NewGuid():N}";
        await InsertHoldDirectAsync(
            expiredHoldId,
            classId,
            student1,
            "HELD",
            DateTimeOffset.UtcNow.AddMinutes(-10));

        // 3. Verify the expired hold frees the seat immediately (lazy expiry)
        using (var classDoc = await GetClassAsync(classId))
        {
            Assert.Equal(0, classDoc.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, classDoc.RootElement.GetProperty("confirmedSeats").GetInt32());
            Assert.Equal(1, classDoc.RootElement.GetProperty("availableSeats").GetInt32());
        }

        // 4. Verify reading the expired hold reports status EXPIRED without mutating database row
        using (var holdResponse = await _client.GetAsync($"/holds/{expiredHoldId}"))
        {
            Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);
            using var doc = JsonDocument.Parse(await holdResponse.Content.ReadAsStringAsync());
            Assert.Equal("EXPIRED", doc.RootElement.GetProperty("status").GetString());
        }

        // 5. Attempt to confirm the expired hold -> must be rejected with 409 Conflict
        using (var confirmResponse = await ConfirmAsync(expiredHoldId, student1))
        {
            Assert.Equal(HttpStatusCode.Conflict, confirmResponse.StatusCode);
            using var doc = JsonDocument.Parse(await confirmResponse.Content.ReadAsStringAsync());
            Assert.Equal("HOLD_EXPIRED", doc.RootElement.GetProperty("code").GetString());
            Assert.Equal("Your hold has expired. Please apply again.", doc.RootElement.GetProperty("message").GetString());
            Assert.False(doc.RootElement.TryGetProperty("stackTrace", out _));
        }

        // 6. Another student can now claim the freed seat successfully
        var student2 = NewStudentId();
        using (var student2Apply = await ApplyAsync(classId, student2))
        {
            Assert.Equal(HttpStatusCode.Created, student2Apply.StatusCode);
            using var doc = JsonDocument.Parse(await student2Apply.Content.ReadAsStringAsync());
            Assert.Equal("HELD", doc.RootElement.GetProperty("status").GetString());
        }

        // Class is now full with Student 2's hold
        using (var classDocAfter = await GetClassAsync(classId))
        {
            Assert.Equal(1, classDocAfter.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, classDocAfter.RootElement.GetProperty("availableSeats").GetInt32());
        }
    }

    /// <summary>
    /// Spec Test 5:
    /// Ownership: Verify that a student cannot confirm or cancel another student's hold
    /// (returns 403 Forbidden).
    /// </summary>
    [Fact]
    public async Task SpecTest5_Ownership_StudentCannotConfirmOrCancelAnotherStudentsHold()
    {
        var classId = await CreateClassAsync("Spec Ownership Class", 5);
        var ownerId = NewStudentId();
        var attackerId = NewStudentId();

        // 1. Owner applies and gets hold
        string holdId;
        using (var applyResponse = await ApplyAsync(classId, ownerId))
        {
            Assert.Equal(HttpStatusCode.Created, applyResponse.StatusCode);
            using var doc = JsonDocument.Parse(await applyResponse.Content.ReadAsStringAsync());
            holdId = doc.RootElement.GetProperty("holdId").GetString()!;
        }

        // 2. Attacker tries to confirm Owner's hold -> 403 Forbidden
        using (var attackerConfirm = await ConfirmAsync(holdId, attackerId))
        {
            Assert.Equal(HttpStatusCode.Forbidden, attackerConfirm.StatusCode);
            using var doc = JsonDocument.Parse(await attackerConfirm.Content.ReadAsStringAsync());
            Assert.Equal("NOT_HOLD_OWNER", doc.RootElement.GetProperty("code").GetString());
            Assert.Equal("This application belongs to a different student.", doc.RootElement.GetProperty("message").GetString());
            Assert.False(doc.RootElement.TryGetProperty("stackTrace", out _));
        }

        // 3. Attacker tries to cancel Owner's hold -> 403 Forbidden
        using (var attackerCancel = await CancelAsync(holdId, attackerId))
        {
            Assert.Equal(HttpStatusCode.Forbidden, attackerCancel.StatusCode);
            using var doc = JsonDocument.Parse(await attackerCancel.Content.ReadAsStringAsync());
            Assert.Equal("NOT_HOLD_OWNER", doc.RootElement.GetProperty("code").GetString());
            Assert.Equal("This application belongs to a different student.", doc.RootElement.GetProperty("message").GetString());
        }

        // Verify hold is still HELD and seat remains held
        using (var holdDoc = await _client.GetAsync($"/holds/{holdId}"))
        {
            using var doc = JsonDocument.Parse(await holdDoc.Content.ReadAsStringAsync());
            Assert.Equal("HELD", doc.RootElement.GetProperty("status").GetString());
        }

        // 4. Owner confirms successfully -> 200 OK
        using (var ownerConfirm = await ConfirmAsync(holdId, ownerId))
        {
            Assert.Equal(HttpStatusCode.OK, ownerConfirm.StatusCode);
        }

        // 5. Attacker tries to confirm already-confirmed hold -> 403 Forbidden (owner check runs before status check)
        using (var attackerConfirmAfter = await ConfirmAsync(holdId, attackerId))
        {
            Assert.Equal(HttpStatusCode.Forbidden, attackerConfirmAfter.StatusCode);
            using var doc = JsonDocument.Parse(await attackerConfirmAfter.Content.ReadAsStringAsync());
            Assert.Equal("NOT_HOLD_OWNER", doc.RootElement.GetProperty("code").GetString());
        }

        // 6. Test cancellation with another hold: Owner can cancel own hold (204 No Content)
        var cancelStudentId = NewStudentId();
        string cancelHoldId;
        using (var applyResponse = await ApplyAsync(classId, cancelStudentId))
        {
            using var doc = JsonDocument.Parse(await applyResponse.Content.ReadAsStringAsync());
            cancelHoldId = doc.RootElement.GetProperty("holdId").GetString()!;
        }

        using (var ownerCancel = await CancelAsync(cancelHoldId, cancelStudentId))
        {
            Assert.Equal(HttpStatusCode.NoContent, ownerCancel.StatusCode);
        }

        // Cancelled hold reports status CANCELLED and seat is released
        using (var holdDoc = await _client.GetAsync($"/holds/{cancelHoldId}"))
        {
            using var doc = JsonDocument.Parse(await holdDoc.Content.ReadAsStringAsync());
            Assert.Equal("CANCELLED", doc.RootElement.GetProperty("status").GetString());
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();

        if (_createdClassIds.Count == 0) return;

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        foreach (var classId in _createdClassIds)
        {
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM holds WHERE class_id = @classId; DELETE FROM classes WHERE id = @classId",
                connection);
            cmd.Parameters.AddWithValue("classId", classId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<string> CreateClassAsync(string name, int totalSeats)
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetString()!;
        _createdClassIds.Add(id);
        return id;
    }

    private async Task<HttpResponseMessage> ApplyAsync(string classId, string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add("X-Student-Id", studentId);
        return await _client.SendAsync(request);
    }

    private async Task<JsonDocument> GetClassAsync(string classId)
    {
        using var response = await _client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<string> GetActiveHoldIdAsync(string classId, string studentId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM holds WHERE class_id = @classId AND student_id = @studentId AND status IN ('HELD', 'CONFIRMED')",
            connection);
        cmd.Parameters.AddWithValue("classId", classId);
        cmd.Parameters.AddWithValue("studentId", studentId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<HttpResponseMessage> ConfirmAsync(string holdId, string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        request.Headers.Add("X-Student-Id", studentId);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> CancelAsync(string holdId, string studentId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{holdId}");
        request.Headers.Add("X-Student-Id", studentId);
        return await _client.SendAsync(request);
    }

    private async Task InsertHoldDirectAsync(
        string holdId,
        string classId,
        string studentId,
        string status,
        DateTimeOffset expiresAt)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO holds (id, class_id, student_id, status, expires_at, created_at) " +
            "VALUES (@id, @classId, @studentId, @status, @expiresAt, now())",
            connection);
        cmd.Parameters.AddWithValue("id", holdId);
        cmd.Parameters.AddWithValue("classId", classId);
        cmd.Parameters.AddWithValue("studentId", studentId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString();

    private static string StudentIdAt(int index) =>
        (20_000_000_000_000_000L + index).ToString("D17");
}
