using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace SchoolAdmission.Tests.Api;

[CollectionDefinition("RequestLogging", DisableParallelization = true)]
public sealed class RequestLoggingCollection;

[Collection("RequestLogging")]
public sealed class RequestLoggingTests
{
    [Fact]
    public async Task OneRequest_EmitsOneRequestLogLine()
    {
        var sink = new CapturingSink();

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:MigrateOnStartup", "false");
                builder.UseSetting(
                    "ConnectionStrings:Default",
                    "Host=localhost;Database=test;Username=test;Password=test");
                builder.ConfigureServices(services => services.AddSingleton<ILogEventSink>(sink));
            });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var requestLogs = sink.Events
            .Where(logEvent =>
                logEvent.Properties.ContainsKey("RequestPath")
                && logEvent.Properties.ContainsKey("StudentId"))
            .ToList();

        Assert.Single(requestLogs);

        var frameworkLogs = sink.Events
            .Where(logEvent => SourceContext(logEvent)?.StartsWith("Microsoft.AspNetCore") == true)
            .Select(logEvent => logEvent.RenderMessage())
            .ToList();

        Assert.True(frameworkLogs.Count == 0, string.Join(Environment.NewLine, frameworkLogs));
    }

    private static string? SourceContext(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out var value)
        && value is ScalarValue { Value: string sourceContext }
            ? sourceContext
            : null;

    private sealed class CapturingSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
