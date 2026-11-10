using SchoolAdmission.Application.Classes;
using SchoolAdmission.Application.Exceptions;

namespace SchoolAdmission.Tests.Application;

public sealed class ClassValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingName(string? name)
    {
        var exception = Assert.Throws<AppException>(
            () => ClassValidator.Validate(name, 10));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Fact]
    public void Validate_RejectsNameLongerThan200Characters()
    {
        var exception = Assert.Throws<AppException>(
            () => ClassValidator.Validate(new string('A', 201), 10));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsNonPositiveTotalSeats(int totalSeats)
    {
        var exception = Assert.Throws<AppException>(
            () => ClassValidator.Validate("Class 10", totalSeats));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Fact]
    public void Validate_TrimsNameAndReturnsNormalizedValues()
    {
        var result = ClassValidator.Validate("  Class 11  ", 12);

        Assert.Equal("Class 11", result.Name);
        Assert.Equal(12, result.TotalSeats);
    }
}
