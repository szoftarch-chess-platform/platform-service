namespace PlatformService.Application.Common.Exceptions;

/// <summary>
/// The request does not prove who the user is, for example a sign-in with a wrong password.
/// Returned to the client as 401 Unauthorized, with <see cref="Exception.Message"/> as the ProblemDetails <c>detail</c>.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
