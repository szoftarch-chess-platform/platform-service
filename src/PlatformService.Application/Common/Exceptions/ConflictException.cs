using System.Collections.ObjectModel;

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

    /// <param name="errors">The messages by the property that conflicts, which become the <c>errors</c> of the ProblemDetails.</param>
    public ConflictException(string message, IReadOnlyDictionary<string, string[]> errors, Exception? innerException = null)
        : base(message, innerException)
    {
        Errors = errors;
    }

    /// <summary>The messages by property name, such as <c>UserName</c>. Empty if the conflict concerns no field.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = ReadOnlyDictionary<string, string[]>.Empty;
}
