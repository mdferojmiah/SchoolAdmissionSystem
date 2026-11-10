namespace SchoolAdmission.Application.Classes;

public sealed record ClassSummary(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);
