using FluentValidation;

namespace PlatformService.Application.Auth;

/// <summary>
/// The rules of the contract for a sign-in. A longer password than at registration can never be right.
/// </summary>
/// <remarks>The JSON body can set a field to <see langword="null"/>, so every rule accepts one and fails on it.</remarks>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.UserName)
            .Must(userName => !string.IsNullOrEmpty(userName))
            .WithMessage(AccountRules.UserNameRequired);

        RuleFor(request => request.Password)
            .Cascade(CascadeMode.Stop)
            .Must(password => !string.IsNullOrEmpty(password))
            .WithMessage(AccountRules.PasswordRequired)
            .Must(password => AccountRules.CodePointLength(password) <= AccountRules.PasswordMaxLength)
            .WithMessage(AccountRules.PasswordTooLong);
    }
}
