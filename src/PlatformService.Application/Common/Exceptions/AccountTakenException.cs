using PlatformService.Application.Auth;

namespace PlatformService.Application.Common.Exceptions;

/// <summary>
/// The user name or the email address of a registration is taken, regardless of case (PS-ADR-11).
/// It names the taken fields, so the client can show the message next to them.
/// </summary>
public sealed class AccountTakenException : ConflictException
{
    public const string UserNameTaken = "The user name is already taken.";
    public const string EmailTaken = "The email address is already taken.";
    public const string BothTaken = "The user name and the email address are already taken.";

    public AccountTakenException(TakenAccountFields fields, Exception? innerException = null)
        : base(CreateMessage(fields), CreateErrors(fields), innerException)
    {
        Fields = fields;
    }

    public TakenAccountFields Fields { get; }

    private static string CreateMessage(TakenAccountFields fields) => fields switch
    {
        TakenAccountFields.UserName => UserNameTaken,
        TakenAccountFields.Email => EmailTaken,
        TakenAccountFields.UserName | TakenAccountFields.Email => BothTaken,
        _ => throw new ArgumentOutOfRangeException(nameof(fields), fields, "At least one field has to be taken."),
    };

    private static Dictionary<string, string[]> CreateErrors(TakenAccountFields fields)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (fields.HasFlag(TakenAccountFields.UserName))
        {
            errors[nameof(RegisterRequest.UserName)] = [UserNameTaken];
        }

        if (fields.HasFlag(TakenAccountFields.Email))
        {
            errors[nameof(RegisterRequest.Email)] = [EmailTaken];
        }

        return errors;
    }
}

/// <summary>The fields of a registration that can be taken.</summary>
[Flags]
public enum TakenAccountFields
{
    None = 0,
    UserName = 1,
    Email = 2,
}
