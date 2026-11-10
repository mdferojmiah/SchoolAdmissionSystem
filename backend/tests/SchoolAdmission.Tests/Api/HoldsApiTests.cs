using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Api;

[Collection("PostgresSchema")]
public sealed class HoldsApiTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly List<string> _createdClassIds = [];

    public HoldsApiTests(PostgresSchemaFixture fixture)
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

    [Fact]
    public async Task Apply_Returns201WithContractShapeAndHeldStatus()
    {
        var classId = await CreateClassAsync("Class 6 - Apply", 10);
        var studentId = NewStudentId();

        using var response = await ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        var holdId = body.GetProperty("holdId").GetString();

        Assert.StartsWith("h_", holdId);
        Assert.Equal(classId, body.GetProperty("classId").GetString());
        Assert.Equal(studentId, body.GetProperty("studentId").GetString());
        Assert.Equal("HELD", body.GetProperty("status").GetString());

        var expiresAt = body.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.InRange(
            expiresAt,
            DateTimeOffset.UtcNow.AddSeconds(590),
            DateTimeOffset.UtcNow.AddSeconds(610));

        Assert.EndsWith($"/holds/{holdId}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Apply_MissingStudentId_Returns400ProblemDetails()
    {
        var classId = await CreateClassAsync("Class 6 - Missing Id", 10);

        using var response = await ApplyAsync(classId, studentId: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_ERROR", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("message").GetString()));
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task Apply_UnknownClass_Returns404ProblemDetails()
    {
        using var response = await ApplyAsync($"c_missing_{Guid.NewGuid():N}", NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLASS_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task Apply_WhenConfirmedSeatsFillTheClass_Returns409NoSeats()
    {
        var classId = await CreateClassAsync("Class 6 - Confirmed Full", 1);
        await InsertHoldAsync(
            classId,
            NewStudentId(),
            "CONFIRMED",
            DateTimeOffset.UtcNow.AddMinutes(10));

        using var response = await ApplyAsync(classId, NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NO_SEATS", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "No seats are available in this class.",
            document.RootElement.GetProperty("message").GetString());
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));

        using var classDocument = await GetClassAsync(classId);
        Assert.Equal(1, classDocument.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, classDocument.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, classDocument.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Apply_WhenHeldAndConfirmedSeatsFillTheClass_Returns409NoSeats()
    {
        var classId = await CreateClassAsync("Class 6 - Held Full", 2);
        await InsertHoldAsync(
            classId,
            NewStudentId(),
            "HELD",
            DateTimeOffset.UtcNow.AddMinutes(10));
        await InsertHoldAsync(
            classId,
            NewStudentId(),
            "CONFIRMED",
            DateTimeOffset.UtcNow.AddMinutes(10));

        using var response = await ApplyAsync(classId, NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NO_SEATS", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Apply_ExpiredHoldFreesSeatWithoutApiCall()
    {
        var classId = await CreateClassAsync("Class 6 - Expired Seat", 1);
        await InsertHoldAsync(
            classId,
            NewStudentId(),
            "HELD",
            DateTimeOffset.UtcNow.AddMinutes(-10));

        using var response = await ApplyAsync(classId, NewStudentId());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var classDocument = await GetClassAsync(classId);
        Assert.Equal(1, classDocument.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, classDocument.RootElement.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Apply_WhenStudentAlreadyHasActiveHold_Returns409Duplicate()
    {
        var classId = await CreateClassAsync("Class 6 - Duplicate", 5);
        var studentId = NewStudentId();

        using (var first = await ApplyAsync(classId, studentId))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var second = await ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var document = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(
            "DUPLICATE_APPLICATION",
            document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "You have already applied to this class.",
            document.RootElement.GetProperty("message").GetString());
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));

        using var classDocument = await GetClassAsync(classId);
        Assert.Equal(1, classDocument.RootElement.GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Apply_WhenStudentAlreadyHasConfirmedAdmission_Returns409Duplicate()
    {
        var classId = await CreateClassAsync("Class 6 - Confirmed Duplicate", 5);
        var studentId = NewStudentId();
        await InsertHoldAsync(
            classId,
            studentId,
            "CONFIRMED",
            DateTimeOffset.UtcNow.AddMinutes(10));

        using var response = await ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "DUPLICATE_APPLICATION",
            document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Apply_AfterOwnHoldExpired_CreatesNewHoldAndMarksOldExpired()
    {
        var classId = await CreateClassAsync("Class 6 - Reapply", 5);
        var studentId = NewStudentId();
        await InsertHoldAsync(
            classId,
            studentId,
            "HELD",
            DateTimeOffset.UtcNow.AddMinutes(-10));

        using var response = await ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var newHoldId = document.RootElement.GetProperty("holdId").GetString();
        Assert.StartsWith("h_", newHoldId);

        var statuses = await CountHoldsByStatusAsync(classId, studentId);
        Assert.Equal(1L, statuses["HELD"]);
        Assert.Equal(1L, statuses["EXPIRED"]);

        using var classDocument = await GetClassAsync(classId);
        Assert.Equal(1, classDocument.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(4, classDocument.RootElement.GetProperty("availableSeats").GetInt32());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();

        if (_createdClassIds.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        foreach (var classId in _createdClassIds)
        {
            await using var command = new NpgsqlCommand(
                "delete from holds where class_id = @class_id; " +
                "delete from classes where id = @id",
                connection);
            command.Parameters.AddWithValue("class_id", classId);
            command.Parameters.AddWithValue("id", classId);

            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task InsertHoldAsync(
        string classId,
        string studentId,
        string status,
        DateTimeOffset expiresAt)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "insert into holds (id, class_id, student_id, status, expires_at, created_at) " +
            "values (@id, @class_id, @student_id, @status, @expires_at, now())",
            connection);
        command.Parameters.AddWithValue("id", $"h_{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("class_id", classId);
        command.Parameters.AddWithValue("student_id", studentId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("expires_at", expiresAt);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<JsonDocument> GetClassAsync(string classId)
    {
        using var response = await _client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<Dictionary<string, long>> CountHoldsByStatusAsync(
        string classId,
        string studentId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "select status, count(*) from holds " +
            "where class_id = @class_id and student_id = @student_id " +
            "group by status",
            connection);
        command.Parameters.AddWithValue("class_id", classId);
        command.Parameters.AddWithValue("student_id", studentId);

        var counts = new Dictionary<string, long>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }

        return counts;
    }

    private async Task<string> CreateClassAsync(string name, int totalSeats)
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        RememberClass(id!);

        return id!;
    }

    private async Task<HttpResponseMessage> ApplyAsync(string classId, string? studentId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/classes/{classId}/holds");

        if (studentId is not null)
        {
            request.Headers.Add("X-Student-Id", studentId);
        }

        return await _client.SendAsync(request);
    }

    private static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString();

    private void RememberClass(string id) => _createdClassIds.Add(id);
}
