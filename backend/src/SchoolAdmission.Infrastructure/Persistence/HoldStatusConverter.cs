using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SchoolAdmission.Domain;

namespace SchoolAdmission.Infrastructure.Persistence;

public static class HoldStatusConverter
{
    public static readonly ValueConverter<HoldStatus, string> Instance = new(
        status => status.ToString().ToUpperInvariant(),
        value => Enum.Parse<HoldStatus>(value, true));
}
