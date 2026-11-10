using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SchoolAdmission.Api.ExceptionHandling;
using SchoolAdmission.Application.Exceptions;

namespace SchoolAdmission.Tests.Api;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task AppException_ReturnsProblemDetailsWithCodeAndMessage()
    {
        var (handler, services) = CreateHandler();
        var context = CreateContext(services);

        var handled = await handler.TryHandleAsync(
            context,
            new AppException(409, "NO_SEATS", "No seats are available in this class."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(409, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        var root = document.RootElement;
        Assert.Equal("NO_SEATS", root.GetProperty("code").GetString());
        Assert.Equal("No seats are available in this class.", root.GetProperty("message").GetString());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.False(root.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task UniqueViolation_MapsToConflict()
    {
        var (handler, services) = CreateHandler();
        var context = CreateContext(services);
        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            "23505");
        var exception = new DbUpdateException("duplicate", postgresException);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(409, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        Assert.Equal("DUPLICATE_APPLICATION", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsGeneric500WithoutStackTraceOrMessage()
    {
        var (handler, services) = CreateHandler();
        var context = CreateContext(services);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("secret internal detail"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        using var document = await ReadJsonAsync(context);
        var root = document.RootElement;
        Assert.Equal("INTERNAL_ERROR", root.GetProperty("code").GetString());
        Assert.DoesNotContain("secret internal detail", root.GetRawText());
        Assert.False(root.TryGetProperty("stackTrace", out _));
    }

    private static (GlobalExceptionHandler Handler, ServiceProvider Services) CreateHandler()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            services.GetRequiredService<IProblemDetailsService>());
        return (handler, services);
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
