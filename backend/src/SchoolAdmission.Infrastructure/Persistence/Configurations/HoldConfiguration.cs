using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

public sealed class HoldConfiguration : IEntityTypeConfiguration<Hold>
{
    public void Configure(EntityTypeBuilder<Hold> builder)
    {
        builder.ToTable("holds", table =>
            table.HasCheckConstraint(
                "ck_holds_status_valid",
                "status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')"));

        builder.HasKey(hold => hold.Id);

        builder.Property(hold => hold.Id)
            .HasColumnName("id")
            .HasMaxLength(64);

        builder.Property(hold => hold.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(hold => hold.StudentId)
            .HasColumnName("student_id")
            .HasMaxLength(17)
            .IsRequired();

        builder.Property(hold => hold.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(HoldStatusConverter.Instance)
            .IsRequired();

        builder.Property(hold => hold.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(hold => hold.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(hold => hold.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.HasOne(hold => hold.Class)
            .WithMany(schoolClass => schoolClass.Holds)
            .HasForeignKey(hold => hold.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(hold => new { hold.ClassId, hold.Status, hold.ExpiresAt })
            .HasDatabaseName("ix_holds_class_id_status_expires_at");

        builder.HasIndex(hold => hold.StudentId)
            .HasDatabaseName("ix_holds_student_id");

        builder.HasIndex(hold => new { hold.ClassId, hold.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_holds_class_id_student_id_active")
            .HasFilter("status IN ('HELD', 'CONFIRMED')");
    }
}
