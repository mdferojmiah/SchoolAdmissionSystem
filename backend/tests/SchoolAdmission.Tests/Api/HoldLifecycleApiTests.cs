using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Api;

[Collection("PostgresSchema")]
public sealed class HoldLifecycleApiTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly List<string> _createdClassIds = [];

    public HoldLifecycleApiTests(PostgresSchemaFixture fixture)
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
    public async Task GetHold_ReturnsContractShapeWithHeldStatus()
    {
        var classId = await CreateClassAsync("Class 6 - Hold Read", 5);
        var holdId = await ApplyAndGetHoldIdAsync(classId, NewStudentId());

        using var response = await _client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;

        Assert.Equal(holdId, body.GetProperty("holdId").GetString());
        Assert.Equal(classId, body.GetProperty("classId").GetString());
        Assert.Equal("HELD", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetHold_UnknownId_Returns404ProblemDetails()
    {
        using var response = await _client.GetAsync($"/holds/h_missing_{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("HOLD_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task GetHold_ExpiredHeld_ReportsExpiredWithoutChangingRow()
    {
        var classId = await CreateClassAsync("Class 6 - Hold Expired Read", 5);
        var holdId = await InsertHoldAsync(
            classId,
            NewStudentId(),
            "HELD",
            DateTimeOffset.UtcNow.AddMinutes(-10));

        using var response = await _client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EXPIRED", document.RootElement.GetProperty("status").GetString());

        var row = await ReadHoldRowAsync(holdId);
        Assert.Equal("HELD", row.Status);
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

    private async Task<string> CreateClassAsync(string name, int totalSeats)
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        _createdClassIds.Add(id!);

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

    private async Task<string> ApplyAndGetHoldIdAsync(string classId, string studentId)
    {
        using var response = await ApplyAsync(classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var holdId = document.RootElement.GetProperty("holdId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(holdId));

        return holdId!;
    }

    private async Task<string> InsertHoldAsync(
        string classId,
        string studentId,
        string status,
        DateTimeOffset expiresAt)
    {
        var holdId = $"h_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "insert into holds (id, class_id, student_id, status, expires_at, created_at) " +
            "values (@id, @class_id, @student_id, @status, @expires_at, now())",
            connection);
        command.Parameters.AddWithValue("id", holdId);
        command.Parameters.AddWithValue("class_id", classId);
        command.Parameters.AddWithValue("student_id", studentId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("expires_at", expiresAt);

        await command.ExecuteNonQueryAsync();

        return holdId;
    }

    private async Task<JsonDocument> GetClassAsync(string classId)
    {
        using var response = await _client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonDocument> GetHoldAsync(string holdId)
    {
        using var response = await _client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<(string Status, bool ConfirmedAtIsSet)> ReadHoldRowAsync(string holdId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "select status, confirmed_at from holds where id = @id",
            connection);
        command.Parameters.AddWithValue("id", holdId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return (reader.GetString(0), !reader.IsDBNull(1));
    }

    private static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString();
}
