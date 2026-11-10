namespace SchoolAdmission.Domain.Entities;

public sealed class SchoolClass
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<Hold> Holds { get; set; } = new List<Hold>();
}
