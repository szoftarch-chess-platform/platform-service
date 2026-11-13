namespace PlatformService.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with the current state, for example a user name that is already taken.
/// Returned to the client as 409 Conflict, with <see cref="Exception.Message"/> as the ProblemDetails <c>detail</c>.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
