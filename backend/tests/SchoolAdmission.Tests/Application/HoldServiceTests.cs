using Npgsql;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Domain;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Tests.Infrastructure;

namespace SchoolAdmission.Tests.Application;

[Collection("PostgresSchema")]
public sealed class HoldServiceTests : IAsyncLifetime
{
    private readonly PostgresSchemaFixture _fixture;
    private readonly List<string> _createdClassIds = [];

    public HoldServiceTests(PostgresSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplyAsync_WhenStudentAlreadyHasActiveHold_ThrowsDuplicateApplication()
    {
        var classId = $"c_{Guid.NewGuid():N}";
        var studentId = NewStudentId();
        RememberClass(classId);

        await using (var setupDb = new AppDbContext(_fixture.Options))
        {
            setupDb.Classes.Add(new SchoolClass
            {
                Id = classId,
                Name = "Class 6 - Service Duplicate",
                TotalSeats = 5,
                CreatedAt = DateTimeOffset.UtcNow
            });
            setupDb.Holds.Add(new Hold
            {
                Id = $"h_{Guid.NewGuid():N}",
                ClassId = classId,
                StudentId = studentId,
                Status = HoldStatus.Held,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                CreatedAt = DateTimeOffset.UtcNow
            });

            await setupDb.SaveChangesAsync();
        }

        await using var serviceDb = new AppDbContext(_fixture.Options);
        var service = new HoldService(serviceDb, new HoldSettings(600));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ApplyAsync(classId, studentId, CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("DUPLICATE_APPLICATION", exception.Code);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
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

    private static string NewStudentId() =>
        Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString();

    private void RememberClass(string id) => _createdClassIds.Add(id);
}
