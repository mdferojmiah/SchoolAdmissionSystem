using SchoolAdmission.Api.Logging;

namespace SchoolAdmission.Tests.Api;

public sealed class StudentIdMaskerTests
{
    [Theory]
    [InlineData(null, "none")]
    [InlineData("", "none")]
    [InlineData("   ", "none")]
    [InlineData("1", "****")]
    [InlineData("1234", "****")]
    [InlineData("20152691234567890", "****7890")]
    public void Mask_KeepsOnlyLastFourDigits(string? studentId, string expected)
    {
        Assert.Equal(expected, StudentIdMasker.Mask(studentId));
    }
}
