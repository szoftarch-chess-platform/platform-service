using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PlatformService.Api.Correlation;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Api.ErrorHandling;

/// <summary>
/// Turns exceptions into RFC 9457 ProblemDetails responses. The single place that maps
/// Application exceptions to HTTP status codes.
/// </summary>
internal sealed partial class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            NotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = exception.Message },
            ConflictException conflictException => CreateConflictProblem(conflictException),
            UnauthorizedException => new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Detail = exception.Message },
            ForbiddenException => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = exception.Message },
            ValidationException validationException => CreateValidationProblem(validationException),
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = environment.IsDevelopment() ? exception.ToString() : null,
            },
        };

        var statusCode = problemDetails.Status!.Value;
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            // The correlation middleware's scope has already ended here, so it is opened again.
            using (RequestCorrelation.BeginScope(logger, httpContext))
            {
                LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
            }
        }

        httpContext.Response.StatusCode = statusCode;

        // If the client accepts no JSON, only the status code is sent and the exception still counts as handled.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });

        return true;
    }

    /// <summary>A conflict that names its fields gets the same <c>errors</c> as a validation problem, keyed the same way.</summary>
    private static ProblemDetails CreateConflictProblem(ConflictException exception)
    {
        var problem = new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = exception.Message };
        if (exception.Errors.Count > 0)
        {
            problem.Extensions["errors"] = exception.Errors.ToDictionary(
                error => ToCamelCasePath(error.Key),
                error => error.Value,
                StringComparer.Ordinal);
        }

        return problem;
    }

    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
    }

    /// <summary>Follows the JSON property names, so <c>Address.PostalCode</c> becomes <c>address.postalCode</c>.</summary>
    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string path);
}
