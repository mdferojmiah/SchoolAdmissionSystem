using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api;
using SchoolAdmission.Api.ExceptionHandling;
using SchoolAdmission.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
