namespace SchoolAdmission.Application.Holds;

public sealed record HoldSummary(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);
