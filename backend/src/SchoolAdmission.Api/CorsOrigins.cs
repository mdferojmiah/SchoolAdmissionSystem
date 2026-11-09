namespace SchoolAdmission.Api;

public static class CorsOrigins
{
    private const string DefaultOrigin = "http://localhost:3000";

    public static string[] Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [DefaultOrigin];
        }

        var origins = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return origins.Length > 0 ? origins : [DefaultOrigin];
    }
}
