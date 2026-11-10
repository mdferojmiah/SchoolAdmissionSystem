using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolAdmission.Tests.Api;

public sealed class ProgramConfigurationTests
{
    [Fact]
    public void MissingConnectionString_ThrowsInvalidOperationException()
    {
        var originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var originalDatabaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
            Environment.SetEnvironmentVariable("DATABASE_URL", null);

            using var factory = new WebApplicationFactory<Program>();

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

            Assert.Contains("ConnectionStrings__Default", exception.Message);
            Assert.Contains("DATABASE_URL", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", originalConnectionString);
            Environment.SetEnvironmentVariable("DATABASE_URL", originalDatabaseUrl);
        }
    }

    [Theory]
    [InlineData("ConnectionStrings:Default")]
    [InlineData("DATABASE_URL")]
    public async Task ConnectionSetting_FromEitherKey_StartsTheApp(string key)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:MigrateOnStartup", "false");
                builder.UseSetting(key, "Host=localhost;Database=test;Username=test;Password=test");
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
