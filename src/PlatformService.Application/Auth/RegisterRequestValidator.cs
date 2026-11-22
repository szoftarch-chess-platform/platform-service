using System.Text.RegularExpressions;
using FluentValidation;

namespace PlatformService.Application.Auth;

/// <summary>
/// The rules of ADR-4 for a new account. Identity checks the uniqueness, because that needs the stored users.
/// </summary>
/// <remarks>The JSON body can set a field to <see langword="null"/>, so every rule accepts one and fails on it.</remarks>
internal sealed partial class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.UserName)
            .Must(userName => userName is { Length: >= AccountRules.UserNameMinLength and <= AccountRules.UserNameMaxLength })
            .WithMessage(AccountRules.UserNameLength);

        // The characters are checked only when there are any, as the frontend does.
        RuleFor(request => request.UserName)
            .Matches(UserNameCharacters())
            .When(request => !string.IsNullOrEmpty(request.UserName))
            .WithMessage(AccountRules.UserNameCharacters);

        RuleFor(request => request.Email)
            .Cascade(CascadeMode.Stop)
            .Must(email => !string.IsNullOrEmpty(email))
            .WithMessage(AccountRules.EmailRequired)
            .Must(email => AccountRules.CodePointLength(email) <= AccountRules.EmailMaxLength)
            .WithMessage(AccountRules.EmailTooLong)
            .EmailAddress()
            .WithMessage(AccountRules.EmailInvalid);

        RuleFor(request => request.Password)
            .Cascade(CascadeMode.Stop)
            .Must(password => AccountRules.CodePointLength(password) >= AccountRules.PasswordMinLength)
            .WithMessage(AccountRules.PasswordTooShort)
            .Must(password => AccountRules.CodePointLength(password) <= AccountRules.PasswordMaxLength)
            .WithMessage(AccountRules.PasswordTooLong);
    }

    // \z instead of $, because in .NET $ also matches before a final line break (PS-ADR-6).
    [GeneratedRegex(@"^[A-Za-z0-9_-]+\z")]
    private static partial Regex UserNameCharacters();
}
