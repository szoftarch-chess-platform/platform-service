using FluentValidation;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Application.Auth;

/// <summary>Use cases that sign a user in and start a session (ADR-4, PS-ADR-12).</summary>
public interface IAuthService
{
    /// <summary>Creates the account and signs the user in.</summary>
    /// <exception cref="ValidationException">The request breaks a rule of ADR-4.</exception>
    /// <exception cref="AccountTakenException">The user name or the email address is taken.</exception>
    Task<SignInResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Signs the user in with the user name and the password.</summary>
    /// <exception cref="ValidationException">The request breaks a rule of the contract.</exception>
    /// <exception cref="UnauthorizedException">The user name is unknown or the password is wrong.</exception>
    Task<SignInResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
