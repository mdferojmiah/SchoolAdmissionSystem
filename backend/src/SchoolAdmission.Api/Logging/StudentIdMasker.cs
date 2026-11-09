namespace SchoolAdmission.Api.Logging;

public static class StudentIdMasker
{
    public static string Mask(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return "none";
        }

        return studentId.Length <= 4 ? "****" : $"****{studentId[^4..]}";
    }
}
