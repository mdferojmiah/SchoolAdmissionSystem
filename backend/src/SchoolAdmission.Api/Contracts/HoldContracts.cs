namespace SchoolAdmission.Api.Contracts;

public sealed record ApplyHoldResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);
