namespace PlatformService.Application.Abstractions;

/// <summary>
/// The user who sent the current request, as identified by the API gateway.
/// Implemented by the presentation layer, so the Application never sees the HTTP context.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Whether the request carries a valid user identity.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user's id. It is a UUID v7 and equals the JWT <c>sub</c> claim.</summary>
    /// <exception cref="InvalidOperationException">The request is not authenticated.</exception>
    Guid UserId { get; }

    /// <summary>The user's user name.</summary>
    /// <exception cref="InvalidOperationException">The request is not authenticated.</exception>
    string UserName { get; }
}
