using SchoolAdmission.Application.Exceptions;

namespace SchoolAdmission.Application.Classes;

public sealed record ValidatedClass(string Name, int TotalSeats);

public static class ClassValidator
{
    public const int MaxNameLength = 200;

    public static ValidatedClass Validate(string? name, int totalSeats)
    {
        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new AppException(
                400,
                "VALIDATION_ERROR",
                "Class name is required.");
        }

        if (trimmedName.Any(char.IsControl))
        {
            throw new AppException(
                400,
                "VALIDATION_ERROR",
                "Class name must not contain control characters.");
        }

        if (trimmedName.Length > MaxNameLength)
        {
            throw new AppException(
                400,
                "VALIDATION_ERROR",
                $"Class name must be {MaxNameLength} characters or fewer.");
        }

        if (totalSeats <= 0)
        {
            throw new AppException(
                400,
                "VALIDATION_ERROR",
                "Total seats must be a positive integer.");
        }

        return new ValidatedClass(trimmedName, totalSeats);
    }
}
