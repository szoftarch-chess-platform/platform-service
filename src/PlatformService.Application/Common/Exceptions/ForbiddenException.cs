namespace PlatformService.Application.Common.Exceptions;

/// <summary>
/// The authenticated user may not perform this operation. This is a domain decision,
/// for example when the user is not a player of the game.
/// Returned to the client as 403 Forbidden, with <see cref="Exception.Message"/> as the ProblemDetails <c>detail</c>.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
