namespace PlatformService.Application.Auth;

/// <summary>
/// The rules of the accounts of ADR-4 with their messages, which are the same as the messages of the frontend (PS-ADR-6).
/// The options of Identity in the Infrastructure take their limits from here too.
/// </summary>
public static class AccountRules
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 20;
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>
    /// The letters of the English alphabet, the digits, the underscore and the hyphen, in the form that Identity takes.
    /// <see cref="RegisterRequestValidator"/> checks the same set with a regular expression.
    /// </summary>
    public const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    public const string UserNameLength = "The user name must be 3 to 20 characters long.";
    public const string UserNameCharacters = "The user name may contain only English letters, digits, _ and -.";
    public const string UserNameRequired = "The user name is required.";
    public const string EmailRequired = "The email address is required.";
    public const string EmailTooLong = "The email address must be at most 254 characters long.";
    public const string EmailInvalid = "The email address is not valid.";
    public const string PasswordTooShort = "The password must be at least 8 characters long.";
    public const string PasswordTooLong = "The password must be at most 128 characters long.";
    public const string PasswordRequired = "The password is required.";

    /// <summary>
    /// The length in Unicode code points, as ADR-4 and the contract measure it, not in the UTF-16 units of
    /// <see cref="string.Length"/>. A missing value counts as empty.
    /// </summary>
    public static int CodePointLength(string? text) => text?.EnumerateRunes().Count() ?? 0;
}
