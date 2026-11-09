using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Exceptions;

namespace SchoolAdmission.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, code, message) = Map(exception);

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = message
        };
        problemDetails.Extensions["code"] = code;
        problemDetails.Extensions["message"] = message;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int StatusCode, string Code, string Message) Map(Exception exception) =>
        exception switch
        {
            AppException appException => (appException.StatusCode, appException.Code, appException.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_APPLICATION", "You have already applied to this class."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred. Please try again.")
        };
}
