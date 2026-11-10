namespace SchoolAdmission.Api.Contracts;

public sealed record ApplyHoldResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record HoldStatusResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTimeOffset ExpiresAt);
