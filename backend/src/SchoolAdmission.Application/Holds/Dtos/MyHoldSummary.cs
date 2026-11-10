namespace SchoolAdmission.Application.Holds;

public sealed record MyHoldSummary(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt);
