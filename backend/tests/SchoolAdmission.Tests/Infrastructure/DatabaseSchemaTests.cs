using Npgsql;

namespace SchoolAdmission.Tests.Infrastructure;

[Collection("PostgresSchema")]
public sealed class DatabaseSchemaTests(PostgresSchemaFixture fixture)
{
    [Fact]
    public async Task Tables_ExistWithExpectedColumns()
    {
        await using var connection = await OpenConnectionAsync();

        var tables = await QueryStringsAsync(
            connection,
            "select table_name from information_schema.tables " +
            "where table_schema = 'public' and table_name in ('classes', 'holds')");

        Assert.Contains("classes", tables);
        Assert.Contains("holds", tables);

        var classColumns = await GetColumnsAsync(connection, "classes");
        Assert.Equal(
            new[] { "created_at", "id", "name", "total_seats" },
            classColumns.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.All(classColumns.Values, column => Assert.Equal("NO", column.IsNullable));
        Assert.Equal("timestamp with time zone", classColumns["created_at"].DataType);

        var holdColumns = await GetColumnsAsync(connection, "holds");
        Assert.Equal(
            new[] { "class_id", "confirmed_at", "created_at", "expires_at", "id", "status", "student_id" },
            holdColumns.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal("YES", holdColumns["confirmed_at"].IsNullable);
        Assert.All(
            holdColumns.Where(column => column.Key != "confirmed_at"),
            column => Assert.Equal("NO", column.Value.IsNullable));
        Assert.Equal("timestamp with time zone", holdColumns["expires_at"].DataType);
        Assert.Equal("timestamp with time zone", holdColumns["created_at"].DataType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Classes_RejectNonPositiveTotalSeats(int totalSeats)
    {
        await using var connection = await OpenConnectionAsync();
        var classId = NewClassId();

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => InsertClassAsync(connection, classId, totalSeats));

            Assert.Equal("23514", exception.SqlState);
        }
        finally
        {
            await DeleteClassAsync(connection, classId);
        }
    }

    [Fact]
    public async Task Holds_PartialUniqueIndexBlocksSecondActiveOrConfirmedHold()
    {
        await using var connection = await OpenConnectionAsync();
        var classId = NewClassId();
        var studentId = NewStudentId();

        try
        {
            await InsertClassAsync(connection, classId, 2);
            var firstHoldId = NewHoldId();
            await InsertHoldAsync(connection, firstHoldId, classId, studentId, "HELD");

            var heldViolation = await Assert.ThrowsAsync<PostgresException>(
                () => InsertHoldAsync(connection, NewHoldId(), classId, studentId, "HELD"));
            Assert.Equal("23505", heldViolation.SqlState);

            await UpdateHoldStatusAsync(connection, firstHoldId, "CONFIRMED");

            var confirmedViolation = await Assert.ThrowsAsync<PostgresException>(
                () => InsertHoldAsync(connection, NewHoldId(), classId, studentId, "HELD"));
            Assert.Equal("23505", confirmedViolation.SqlState);
        }
        finally
        {
            await DeleteHoldsAsync(connection, classId);
            await DeleteClassAsync(connection, classId);
        }
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("EXPIRED")]
    public async Task Holds_AllowReapplyAfterCancelledOrExpired(string terminalStatus)
    {
        await using var connection = await OpenConnectionAsync();
        var classId = NewClassId();
        var studentId = NewStudentId();

        try
        {
            await InsertClassAsync(connection, classId, 2);
            var firstHoldId = NewHoldId();
            await InsertHoldAsync(connection, firstHoldId, classId, studentId, "HELD");
            await UpdateHoldStatusAsync(connection, firstHoldId, terminalStatus);

            await InsertHoldAsync(connection, NewHoldId(), classId, studentId, "HELD");

            Assert.Equal(2, await CountHoldsAsync(connection, classId));
        }
        finally
        {
            await DeleteHoldsAsync(connection, classId);
            await DeleteClassAsync(connection, classId);
        }
    }

    [Fact]
    public async Task Holds_RejectMissingClassForeignKey()
    {
        await using var connection = await OpenConnectionAsync();
        var holdId = NewHoldId();

        try
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => InsertHoldAsync(connection, holdId, "c_missing", NewStudentId(), "HELD"));

            Assert.Equal("23503", exception.SqlState);
        }
        finally
        {
            await ExecuteAsync(connection, "delete from holds where id = @id", ("id", holdId));
        }
    }

    [Fact]
    public async Task Schema_HasSeatCountingAndStudentIndexes()
    {
        await using var connection = await OpenConnectionAsync();

        var indexes = await GetIndexesAsync(connection, "holds");

        Assert.Contains("ux_holds_class_id_student_id_active", indexes.Keys);
        var uniqueIndex = indexes["ux_holds_class_id_student_id_active"];
        Assert.Contains("UNIQUE", uniqueIndex);
        Assert.Contains("class_id", uniqueIndex);
        Assert.Contains("student_id", uniqueIndex);
        Assert.Contains("HELD", uniqueIndex);
        Assert.Contains("CONFIRMED", uniqueIndex);

        Assert.Contains("ix_holds_class_id_status_expires_at", indexes.Keys);
        var seatIndex = indexes["ix_holds_class_id_status_expires_at"];
        Assert.Contains("class_id", seatIndex);
        Assert.Contains("status", seatIndex);
        Assert.Contains("expires_at", seatIndex);

        Assert.Contains("ix_holds_student_id", indexes.Keys);
        Assert.Contains("student_id", indexes["ix_holds_student_id"]);
    }

    [Fact]
    public async Task Data_IsReadableFromANewContext()
    {
        var classId = NewClassId();

        try
        {
            await using (var firstConnection = await OpenConnectionAsync())
            {
                await InsertClassAsync(firstConnection, classId, 7);
            }

            await using var secondConnection = await OpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                "select name, total_seats from classes where id = @id",
                secondConnection);
            command.Parameters.AddWithValue("id", classId);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Persistence Check", reader.GetString(0));
            Assert.Equal(7, reader.GetInt32(1));
        }
        finally
        {
            await using var cleanupConnection = await OpenConnectionAsync();
            await DeleteClassAsync(cleanupConnection, classId);
        }
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string NewClassId() => $"c_{Guid.NewGuid():N}";

    private static string NewHoldId() => $"h_{Guid.NewGuid():N}";

    private static string NewStudentId() => Guid.NewGuid().ToString("N")[..17];

    private static async Task InsertClassAsync(
        NpgsqlConnection connection,
        string classId,
        int totalSeats)
    {
        await ExecuteAsync(
            connection,
            "insert into classes (id, name, total_seats, created_at) " +
            "values (@id, @name, @total_seats, now())",
            ("id", classId),
            ("name", "Persistence Check"),
            ("total_seats", totalSeats));
    }

    private static async Task InsertHoldAsync(
        NpgsqlConnection connection,
        string holdId,
        string classId,
        string studentId,
        string status)
    {
        await ExecuteAsync(
            connection,
            "insert into holds (id, class_id, student_id, status, expires_at, created_at) " +
            "values (@id, @class_id, @student_id, @status, now() + interval '10 minutes', now())",
            ("id", holdId),
            ("class_id", classId),
            ("student_id", studentId),
            ("status", status));
    }

    private static async Task UpdateHoldStatusAsync(
        NpgsqlConnection connection,
        string holdId,
        string status)
    {
        await ExecuteAsync(
            connection,
            "update holds set status = @status where id = @id",
            ("status", status),
            ("id", holdId));
    }

    private static async Task<int> CountHoldsAsync(NpgsqlConnection connection, string classId)
    {
        await using var command = new NpgsqlCommand(
            "select count(*) from holds where class_id = @class_id",
            connection);
        command.Parameters.AddWithValue("class_id", classId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task DeleteHoldsAsync(NpgsqlConnection connection, string classId)
    {
        await ExecuteAsync(connection, "delete from holds where class_id = @class_id", ("class_id", classId));
    }

    private static async Task DeleteClassAsync(NpgsqlConnection connection, string classId)
    {
        await ExecuteAsync(connection, "delete from classes where id = @id", ("id", classId));
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> QueryStringsAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<Dictionary<string, ColumnInfo>> GetColumnsAsync(
        NpgsqlConnection connection,
        string tableName)
    {
        await using var command = new NpgsqlCommand(
            "select column_name, data_type, is_nullable from information_schema.columns " +
            "where table_schema = 'public' and table_name = @table_name",
            connection);
        command.Parameters.AddWithValue("table_name", tableName);

        await using var reader = await command.ExecuteReaderAsync();

        var columns = new Dictionary<string, ColumnInfo>();
        while (await reader.ReadAsync())
        {
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(1), reader.GetString(2));
        }

        return columns;
    }

    private static async Task<Dictionary<string, string>> GetIndexesAsync(
        NpgsqlConnection connection,
        string tableName)
    {
        await using var command = new NpgsqlCommand(
            "select indexname, indexdef from pg_indexes " +
            "where schemaname = 'public' and tablename = @table_name",
            connection);
        command.Parameters.AddWithValue("table_name", tableName);

        await using var reader = await command.ExecuteReaderAsync();

        var indexes = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            indexes[reader.GetString(0)] = reader.GetString(1);
        }

        return indexes;
    }

    private sealed record ColumnInfo(string DataType, string IsNullable);
}
