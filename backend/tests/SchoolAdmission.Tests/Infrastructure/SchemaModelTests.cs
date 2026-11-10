using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SchoolAdmission.Domain;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Tests.Infrastructure;

public sealed class SchemaModelTests
{
    [Fact]
    public void Model_MapsEntitiesToExpectedTablesAndColumns()
    {
        using var db = CreateContext();
        var model = DesignTimeModel(db);

        var classEntity = model.FindEntityType(typeof(SchoolClass));
        Assert.NotNull(classEntity);
        Assert.Equal("classes", classEntity.GetTableName());
        Assert.Equal(
            new[] { "created_at", "id", "name", "total_seats" },
            ColumnNames(classEntity).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Contains(
            classEntity.GetCheckConstraints(),
            constraint => constraint.Sql.Contains("total_seats > 0"));

        var holdEntity = model.FindEntityType(typeof(Hold));
        Assert.NotNull(holdEntity);
        Assert.Equal("holds", holdEntity.GetTableName());
        Assert.Equal(
            new[]
            {
                "class_id", "confirmed_at", "created_at", "expires_at", "id", "status", "student_id"
            },
            ColumnNames(holdEntity).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var studentId = holdEntity.FindProperty(nameof(Hold.StudentId));
        Assert.NotNull(studentId);
        Assert.Equal(17, studentId.GetMaxLength());

        var status = holdEntity.FindProperty(nameof(Hold.Status));
        Assert.NotNull(status);
        Assert.Equal(16, status.GetMaxLength());
    }

    [Fact]
    public void Holds_HasPartialUniqueIndexAndSupportingIndexes()
    {
        using var db = CreateContext();
        var holdEntity = DesignTimeModel(db).FindEntityType(typeof(Hold));
        Assert.NotNull(holdEntity);

        var indexes = holdEntity.GetIndexes().ToList();

        var uniqueIndex = Assert.Single(
            indexes,
            index => index.IsUnique
                && ColumnNames(index).SequenceEqual(new[] { "class_id", "student_id" }));
        Assert.Equal("status IN ('HELD', 'CONFIRMED')", uniqueIndex.GetFilter());
        Assert.Equal("ux_holds_class_id_student_id_active", uniqueIndex.GetDatabaseName());

        var seatIndex = Assert.Single(
            indexes,
            index => !index.IsUnique
                && ColumnNames(index).SequenceEqual(new[] { "class_id", "status", "expires_at" }));
        Assert.Equal("ix_holds_class_id_status_expires_at", seatIndex.GetDatabaseName());

        var studentIndex = Assert.Single(
            indexes,
            index => !index.IsUnique
                && ColumnNames(index).SequenceEqual(new[] { "student_id" }));
        Assert.Equal("ix_holds_student_id", studentIndex.GetDatabaseName());
    }

    [Fact]
    public void HoldStatusConverter_StoresUppercaseValues()
    {
        Assert.Equal("HELD", HoldStatusConverter.Instance.ConvertToProvider(HoldStatus.Held));
        Assert.Equal("CONFIRMED", HoldStatusConverter.Instance.ConvertToProvider(HoldStatus.Confirmed));
        Assert.Equal(HoldStatus.Confirmed, HoldStatusConverter.Instance.ConvertFromProvider("CONFIRMED"));
        Assert.Equal(HoldStatus.Expired, HoldStatusConverter.Instance.ConvertFromProvider("EXPIRED"));
    }

    [Fact]
    public void Holds_HasRestrictForeignKeyToClasses()
    {
        using var db = CreateContext();
        var holdEntity = DesignTimeModel(db).FindEntityType(typeof(Hold));
        Assert.NotNull(holdEntity);

        var foreignKey = Assert.Single(holdEntity.GetForeignKeys());

        Assert.Equal("classes", foreignKey.PrincipalEntityType.GetTableName());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options);

    private static IModel DesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    private static IEnumerable<string?> ColumnNames(IEntityType entityType) =>
        entityType.GetProperties().Select(property => property.GetColumnName());

    private static IEnumerable<string?> ColumnNames(IIndex index) =>
        index.Properties.Select(property => property.GetColumnName());
}
