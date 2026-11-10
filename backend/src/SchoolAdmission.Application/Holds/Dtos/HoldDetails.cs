namespace SchoolAdmission.Application.Holds;

public sealed record HoldDetails(
    string HoldId,
    string ClassId,
    string Status,
    DateTimeOffset ExpiresAt);
