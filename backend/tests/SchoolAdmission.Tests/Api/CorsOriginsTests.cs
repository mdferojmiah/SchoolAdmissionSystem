using SchoolAdmission.Api;

namespace SchoolAdmission.Tests.Api;

public sealed class CorsOriginsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_FallsBackToDefaultOrigin(string? raw)
    {
        Assert.Equal(new[] { "http://localhost:3000" }, CorsOrigins.Parse(raw));
    }

    [Fact]
    public void Parse_SplitsTrimsAndDeduplicates()
    {
        var origins = CorsOrigins.Parse(" http://localhost:3000, http://localhost:5173 ,http://localhost:3000 ");

        Assert.Equal(new[] { "http://localhost:3000", "http://localhost:5173" }, origins);
    }
}
