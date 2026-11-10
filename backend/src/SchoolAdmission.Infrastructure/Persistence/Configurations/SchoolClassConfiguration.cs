using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure.Persistence.Configurations;

public sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("classes", table =>
            table.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));

        builder.HasKey(schoolClass => schoolClass.Id);

        builder.Property(schoolClass => schoolClass.Id)
            .HasColumnName("id")
            .HasMaxLength(64);

        builder.Property(schoolClass => schoolClass.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(schoolClass => schoolClass.TotalSeats)
            .HasColumnName("total_seats")
            .IsRequired();

        builder.Property(schoolClass => schoolClass.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
