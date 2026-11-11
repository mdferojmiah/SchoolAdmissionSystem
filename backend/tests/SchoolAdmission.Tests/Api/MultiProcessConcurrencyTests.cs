using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Api.ExceptionHandling;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Persistence;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Api;

/// <summary>
/// Bonus Verification (FER-15 / Architecture Section 8 / TASKS.md):
/// Proves that running two backend processes on different ports against the same database
/// guarantees held + confirmed seats never exceed total seats (Rule R1).
/// </summary>
[Collection("PostgresSchema")]
public sealed class MultiProcessConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private WebApplication? _serverA;
    private WebApplication? _serverB;
    private HttpClient? _clientA;
    private HttpClient? _clientB;
    private readonly List<string> _createdClassIds = [];

    // Distinct TCP ports for the two backend server processes
    private const int PortA = 5101;
    private const int PortB = 5102;
    private readonly string _baseUrlA = $"http://127.0.0.1:{PortA}";
    private readonly string _baseUrlB = $"http://127.0.0.1:{PortB}";

    public MultiProcessConcurrencyTests(PostgresSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _serverA = await StartKestrelServerAsync(PortA, _fixture.ConnectionString);
        _serverB = await StartKestrelServerAsync(PortB, _fixture.ConnectionString);

        _clientA = new HttpClient { BaseAddress = new Uri(_baseUrlA) };
        _clientB = new HttpClient { BaseAddress = new Uri(_baseUrlB) };
    }

    public async Task DisposeAsync()
    {
        _clientA?.Dispose();
        _clientB?.Dispose();

        if (_createdClassIds.Count > 0)
        {
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            foreach (var classId in _createdClassIds)
            {
                await using var cmd = new NpgsqlCommand("DELETE FROM holds WHERE class_id = @id; DELETE FROM classes WHERE id = @id;", connection);
                cmd.Parameters.AddWithValue("id", classId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        if (_serverA != null)
        {
            await _serverA.StopAsync();
            await _serverA.DisposeAsync();
        }

        if (_serverB != null)
        {
            await _serverB.StopAsync();
            await _serverB.DisposeAsync();
        }
    }

    private static async Task<WebApplication> StartKestrelServerAsync(int port, string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddScoped<ClassService>();
        builder.Services.AddSingleton(new HoldSettings(600));
        builder.Services.AddScoped<HoldService>();
        builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        builder.Services.AddControllers().AddApplicationPart(typeof(Program).Assembly);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .AllowAnyOrigin()
                .WithHeaders("X-Student-Id", "Content-Type")
                .WithMethods("GET", "POST", "DELETE", "OPTIONS")
                .WithExposedHeaders("Date"));
        });

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseCors();
        app.MapControllers();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task TwoBackendProcessesOnDifferentPorts_ConcurrentApplies_NeverExceedTotalSeats()
    {
        const int totalSeats = 5;
        const int totalAttempts = 30; // 15 requests routed to PortA, 15 to PortB

        // Create a class via Server A (Port 5101)
        var createResponse = await _clientA!.PostAsJsonAsync("/classes", new { name = "Multi-Process Bonus Class", totalSeats });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var classId = createDoc.RootElement.GetProperty("id").GetString()!;
        _createdClassIds.Add(classId);

        // Fire 30 concurrent apply requests interleaved across Port 5101 (Server A) and Port 5102 (Server B)
        var tasks = Enumerable.Range(0, totalAttempts).Select(i =>
        {
            var studentId = $"20261111{i:D9}";
            var client = (i % 2 == 0) ? _clientA! : _clientB!;
            var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
            request.Headers.Add("X-Student-Id", studentId);
            return client.SendAsync(request);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        try
        {
            var statuses = responses.Select(r => r.StatusCode).ToList();
            var successfulApplies = statuses.Count(s => s == HttpStatusCode.Created);
            var rejectedApplies = statuses.Count(s => s == HttpStatusCode.Conflict);

            // Exactly 5 succeeds, 25 rejected, 0 500s
            Assert.Equal(totalSeats, successfulApplies);
            Assert.Equal(totalAttempts - totalSeats, rejectedApplies);
            Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);

            // Verify rejected responses have ProblemDetails code "NO_SEATS"
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

        // Verify seat counts on Server A (Port 5101)
        using var docA = JsonDocument.Parse(await _clientA!.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(totalSeats, docA.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, docA.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, docA.RootElement.GetProperty("availableSeats").GetInt32());

        // Verify seat counts on Server B (Port 5102)
        using var docB = JsonDocument.Parse(await _clientB!.GetStringAsync($"/classes/{classId}"));
        Assert.Equal(totalSeats, docB.RootElement.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, docB.RootElement.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, docB.RootElement.GetProperty("availableSeats").GetInt32());

        // Verify active holds directly in PostgreSQL
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM holds WHERE class_id = @classId AND status = 'HELD' AND expires_at > now()",
            connection);
        cmd.Parameters.AddWithValue("classId", classId);

        var activeHolds = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal((long)totalSeats, activeHolds);
    }
}
