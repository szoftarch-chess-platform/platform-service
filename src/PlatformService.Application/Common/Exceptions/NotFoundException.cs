namespace PlatformService.Application.Common.Exceptions;

/// <summary>
/// The requested resource does not exist. Returned to the client as 404 Not Found,
/// with <see cref="Exception.Message"/> as the ProblemDetails <c>detail</c>.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
