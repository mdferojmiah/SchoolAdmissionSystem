using SchoolAdmission.Application.Exceptions;

namespace SchoolAdmission.Application.Holds;

public static class StudentIdValidator
{
    public static string Validate(string? studentId)
    {
        if (studentId is null
            || studentId.Length != 17
            || !studentId.All(character => character is >= '0' and <= '9'))
        {
            throw new AppException(
                400,
                "VALIDATION_ERROR",
                "Student id must be exactly 17 digits.");
        }

        return studentId;
    }
}
