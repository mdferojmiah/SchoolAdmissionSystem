using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using SchoolAdmission.Api;
using SchoolAdmission.Api.ExceptionHandling;
using SchoolAdmission.Api.Logging;
using SchoolAdmission.Api.Startup;
using SchoolAdmission.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["DATABASE_URL"]
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings__Default or DATABASE_URL for the PostgreSQL connection.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var corsOrigins = CorsOrigins.Parse(builder.Configuration["CORS_ORIGINS"]);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .WithHeaders("X-Student-Id", "Content-Type")
        .WithMethods("GET", "POST", "DELETE", "OPTIONS")
        .WithExposedHeaders("Date"));
});

var app = builder.Build();

if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await DatabaseMigrator.ApplyMigrationsAsync(app.Services);
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms (student {StudentId})";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var studentId = httpContext.Request.Headers["X-Student-Id"].FirstOrDefault();
        diagnosticContext.Set("StudentId", StudentIdMasker.Mask(studentId));
    };

    options.GetLevel = (httpContext, _, exception) =>
        exception is not null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
});

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
