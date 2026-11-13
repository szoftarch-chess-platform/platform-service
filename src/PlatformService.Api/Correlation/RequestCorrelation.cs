namespace PlatformService.Api.Correlation;

/// <summary>
/// The correlation id that ties the service's logs and error responses to the gateway's access log.
/// </summary>
/// <remarks>
/// It is called <c>CorrelationId</c> because ASP.NET Core's own logging scope already has a <c>RequestId</c>,
/// which holds the server side <see cref="HttpContext.TraceIdentifier"/>.
/// </remarks>
internal static class RequestCorrelation
{
    /// <summary>Set by the gateway on every request.</summary>
    public const string HeaderName = "x-request-id";

    private static readonly Func<ILogger, string, IDisposable?> _correlationIdScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    /// <summary>The gateway's <c>x-request-id</c>, or <see cref="HttpContext.TraceIdentifier"/> if it is missing.</summary>
    public static string GetCorrelationId(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers[HeaderName].ToString();
        return string.IsNullOrWhiteSpace(header) ? httpContext.TraceIdentifier : header;
    }

    /// <summary>Starts a logging scope with a <c>CorrelationId</c> property.</summary>
    public static IDisposable? BeginScope(ILogger logger, HttpContext httpContext) =>
        _correlationIdScope(logger, GetCorrelationId(httpContext));

    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestCorrelationMiddleware>();
}

/// <summary>Wraps the rest of the pipeline in the <c>CorrelationId</c> logging scope.</summary>
internal sealed class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (RequestCorrelation.BeginScope(logger, context))
        {
            await next(context);
        }
    }
}
