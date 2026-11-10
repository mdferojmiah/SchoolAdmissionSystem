using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Api;

[Collection("PostgresSchema")]
public sealed class ClassesApiTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly List<string> _createdClassIds = [];

    public ClassesApiTests(PostgresSchemaFixture fixture)
    {
        _fixture = fixture;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("ConnectionStrings:Default", fixture.ConnectionString);
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateClass_Returns201WithContractShape()
    {
        using var response = await _client.PostAsJsonAsync("/classes", new
        {
            name = "Class 6 - Morning Shift",
            totalSeats = 120
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        var id = body.GetProperty("id").GetString();
        RememberClass(id!);

        Assert.StartsWith("c_", id);
        Assert.Equal("Class 6 - Morning Shift", body.GetProperty("name").GetString());
        Assert.Equal(120, body.GetProperty("totalSeats").GetInt32());
        Assert.EndsWith($"/classes/{id}", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Class 10", 0)]
    [InlineData("Class 10", -1)]
    public async Task CreateClass_InvalidInput_Returns400ProblemDetails(
        string? name,
        int totalSeats)
    {
        using var response = await _client.PostAsJsonAsync("/classes", new { name, totalSeats });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_ERROR", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("message").GetString()));
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task CreateClass_NameLongerThan200Characters_Returns400()
    {
        using var response = await _client.PostAsJsonAsync("/classes", new
        {
            name = new string('A', 201),
            totalSeats = 10
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateClass_TrimsName()
    {
        using var response = await _client.PostAsJsonAsync("/classes", new
        {
            name = "  Class 11  ",
            totalSeats = 12
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        RememberClass(document.RootElement.GetProperty("id").GetString()!);
        Assert.Equal("Class 11", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetClass_ReturnsSeatSummaryWithZeroHolds()
    {
        var id = await CreateClassAsync("Class 7", 30);

        using var document = await GetClassAsync(id);
        var body = document.RootElement;

        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("Class 7", body.GetProperty("name").GetString());
        Assert.Equal(30, body.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, body.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, body.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(30, body.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task GetClass_UnknownId_Returns404ProblemDetails()
    {
        using var response = await _client.GetAsync($"/classes/c_missing_{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CLASS_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task GetClasses_ReturnsCreatedClassWithSeatSummary()
    {
        var id = await CreateClassAsync("Class 8 - Morning Shift", 40);

        using var response = await _client.GetAsync("/classes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var posted = document.RootElement
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == id);

        Assert.Equal("Class 8 - Morning Shift", posted.GetProperty("name").GetString());
        Assert.Equal(40, posted.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, posted.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, posted.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(40, posted.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task GetClass_CountsOnlyUnexpiredHeldAndAllConfirmed()
    {
        var id = await CreateClassAsync("Class 9 - Counts", 10);

        await InsertHoldAsync(id, "HELD", DateTimeOffset.UtcNow.AddMinutes(10));
        await InsertHoldAsync(id, "HELD", DateTimeOffset.UtcNow.AddMinutes(-10));
        await InsertHoldAsync(id, "CONFIRMED", DateTimeOffset.UtcNow.AddMinutes(10));
        await InsertHoldAsync(id, "CONFIRMED", DateTimeOffset.UtcNow.AddMinutes(-10));
        await InsertHoldAsync(id, "CANCELLED", DateTimeOffset.UtcNow.AddMinutes(10));
        await InsertHoldAsync(id, "EXPIRED", DateTimeOffset.UtcNow.AddMinutes(-10));

        using var detail = await GetClassAsync(id);
        Assert.Equal(1, detail.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, detail.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(7, detail.RootElement.GetProperty("availableSeats").GetInt32());

        using var listResponse = await _client.GetAsync("/classes");
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listed = list.RootElement
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == id);

        Assert.Equal(1, listed.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, listed.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(7, listed.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task GetClass_ExpiredHoldFreesSeatWithoutApiCall()
    {
        var id = await CreateClassAsync("Class 9 - Expiry", 2);
        await InsertHoldAsync(id, "HELD", DateTimeOffset.UtcNow.AddMinutes(10));

        using (var before = await GetClassAsync(id))
        {
            Assert.Equal(1, before.RootElement.GetProperty("heldSeats").GetInt32());
            Assert.Equal(1, before.RootElement.GetProperty("availableSeats").GetInt32());
        }

        await ExpireHoldsAsync(id);

        using var after = await GetClassAsync(id);
        Assert.Equal(0, after.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, after.RootElement.GetProperty("availableSeats").GetInt32());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await DeleteCreatedClassesAsync();
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

    private async Task<JsonDocument> GetClassAsync(string classId)
    {
        using var response = await _client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task InsertHoldAsync(string classId, string status, DateTimeOffset expiresAt)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "insert into holds (id, class_id, student_id, status, expires_at, created_at) " +
            "values (@id, @class_id, @student_id, @status, @expires_at, now())",
            connection);
        command.Parameters.AddWithValue("id", $"h_{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("class_id", classId);
        command.Parameters.AddWithValue("student_id", NewStudentId());
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("expires_at", expiresAt);

        await command.ExecuteNonQueryAsync();
    }

    private async Task ExpireHoldsAsync(string classId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "update holds set expires_at = now() - interval '1 minute' where class_id = @class_id",
            connection);
        command.Parameters.AddWithValue("class_id", classId);

        await command.ExecuteNonQueryAsync();
    }

    private static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString();

    private void RememberClass(string id) => _createdClassIds.Add(id);

    private async Task DeleteCreatedClassesAsync()
    {
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
}
