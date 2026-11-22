using FluentValidation;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;
using PlatformService.Application.Users;
using PlatformService.Domain.Sessions;

namespace PlatformService.Application.Auth;

internal sealed class AuthService(
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IUserAccounts userAccounts,
    ISessionRepository sessions,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IAuthService
{
    /// <summary>The same for an unknown user name and a wrong password, so the response does not tell which one.</summary>
    public const string WrongCredentials = "The user name or the password is wrong.";

    public async Task<SignInResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var account = await userAccounts.CreateAsync(
            Guid.CreateVersion7(now), request.UserName, request.Email, request.Password, cancellationToken);

        // The account and its session are saved in the same commit (PS-ADR-11).
        return await StartSessionAsync(account, now, cancellationToken);
    }

    public async Task<SignInResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var account = await userAccounts.FindByCredentialsAsync(request.UserName, request.Password, cancellationToken)
            ?? throw new UnauthorizedException(WrongCredentials);

        return await StartSessionAsync(account, timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<SignInResult> StartSessionAsync(UserAccount account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokens.Create();
        sessions.Add(Session.Start(account.Id, refreshToken.Hash, now));
        var accessToken = accessTokens.Issue(account, now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SignInResult(
            new CurrentUserDto(account.Id, account.UserName),
            accessToken,
            new IssuedToken(refreshToken.Value, RefreshToken.Lifetime));
    }
}
