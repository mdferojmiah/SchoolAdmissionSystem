using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Holds;

namespace SchoolAdmission.Tests.Application;

public sealed class StudentIdValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890123456")]
    [InlineData("123456789012345678")]
    [InlineData("1234567890123456A")]
    [InlineData(" 1234567890123456")]
    public void Validate_RejectsAnythingButExactly17Digits(string? studentId)
    {
        var exception = Assert.Throws<AppException>(
            () => StudentIdValidator.Validate(studentId));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("VALIDATION_ERROR", exception.Code);
    }

    [Fact]
    public void Validate_Accepts17Digits()
    {
        var result = StudentIdValidator.Validate("20152691234567890");

        Assert.Equal("20152691234567890", result);
    }
}
