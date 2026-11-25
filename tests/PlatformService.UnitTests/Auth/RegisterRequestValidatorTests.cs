using PlatformService.Application.Auth;

namespace PlatformService.UnitTests.Auth;

public sealed class RegisterRequestValidatorTests
{
    private const string Emoji = "\U0001F600";

    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void AValidRequest_HasNoErrors()
    {
        Assert.True(_validator.Validate(Request()).IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcdefghijklmnopqrst")]
    [InlineData("Alice_01-x")]
    public void UserName_WithinTheRules_IsValid(string userName)
    {
        Assert.Empty(Messages(Request(userName: userName), nameof(RegisterRequest.UserName)));
    }

    [Theory]
    [InlineData("ab", AccountRules.UserNameLength)]
    [InlineData("abcdefghijklmnopqrstu", AccountRules.UserNameLength)]
    [InlineData("", AccountRules.UserNameLength)]
    [InlineData(null, AccountRules.UserNameLength)]
    [InlineData("al.ice", AccountRules.UserNameCharacters)]
    [InlineData("al ice", AccountRules.UserNameCharacters)]
    [InlineData("álice", AccountRules.UserNameCharacters)]
    // In .NET $ would match before the final line break, so this one would pass with $.
    [InlineData("alice\n", AccountRules.UserNameCharacters)]
    public void UserName_AgainstTheRules_HasItsMessage(string? userName, string message)
    {
        Assert.Equal([message], Messages(Request(userName: userName), nameof(RegisterRequest.UserName)));
    }

    [Fact]
    public void UserName_TooShortWithWrongCharacters_HasBothMessages()
    {
        Assert.Equal(
            [AccountRules.UserNameLength, AccountRules.UserNameCharacters],
            Messages(Request(userName: "a."), nameof(RegisterRequest.UserName)));
    }

    // The frontend checks the same rule, which takes any character except whitespace around one @.
    [Theory]
    [InlineData("a@b")]
    [InlineData("álmos@példa.hu")]
    public void Email_WithinTheRules_IsValid(string email)
    {
        Assert.Empty(Messages(Request(email: email), nameof(RegisterRequest.Email)));
    }

    [Theory]
    [InlineData("", AccountRules.EmailRequired)]
    [InlineData(null, AccountRules.EmailRequired)]
    [InlineData("alice", AccountRules.EmailInvalid)]
    [InlineData("alice@", AccountRules.EmailInvalid)]
    [InlineData("@example.com", AccountRules.EmailInvalid)]
    [InlineData("alice@@example.com", AccountRules.EmailInvalid)]
    [InlineData("alice @example.com", AccountRules.EmailInvalid)]
    [InlineData("alice@example .com", AccountRules.EmailInvalid)]
    // In .NET $ would match before the final line break, so this one would pass with $.
    [InlineData("alice@example.com\n", AccountRules.EmailInvalid)]
    public void Email_AgainstTheRules_HasItsMessage(string? email, string message)
    {
        Assert.Equal([message], Messages(Request(email: email), nameof(RegisterRequest.Email)));
    }

    [Fact]
    public void Email_CountsCodePoints()
    {
        // 254 code points, but 2 more UTF-16 units.
        var atTheLimit = $"{Emoji}{Emoji}{new string('a', 240)}@example.com";
        var overTheLimit = $"a{atTheLimit}";

        Assert.Empty(Messages(Request(email: atTheLimit), nameof(RegisterRequest.Email)));
        Assert.Equal([AccountRules.EmailTooLong], Messages(Request(email: overTheLimit), nameof(RegisterRequest.Email)));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public void Password_WithinTheLengths_IsValid(int length)
    {
        Assert.Empty(Messages(Request(password: new string('p', length)), nameof(RegisterRequest.Password)));
    }

    [Theory]
    [InlineData("1234567", AccountRules.PasswordTooShort)]
    [InlineData("", AccountRules.PasswordTooShort)]
    [InlineData(null, AccountRules.PasswordTooShort)]
    public void Password_TooShort_HasItsMessage(string? password, string message)
    {
        Assert.Equal([message], Messages(Request(password: password), nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void Password_TooLong_HasItsMessage()
    {
        Assert.Equal(
            [AccountRules.PasswordTooLong],
            Messages(Request(password: new string('p', 129)), nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void Password_CountsCodePoints()
    {
        // 7 code points are too few, although they are 14 UTF-16 units, and 128 are enough although they are 256.
        var tooShort = string.Concat(Enumerable.Repeat(Emoji, 7));
        var longest = string.Concat(Enumerable.Repeat(Emoji, 128));

        Assert.Equal([AccountRules.PasswordTooShort], Messages(Request(password: tooShort), nameof(RegisterRequest.Password)));
        Assert.Empty(Messages(Request(password: longest), nameof(RegisterRequest.Password)));
    }

    private static RegisterRequest Request(
        string? userName = "alice",
        string? email = "alice@example.com",
        string? password = "correct horse battery") =>
        new() { UserName = userName!, Email = email!, Password = password! };

    private string[] Messages(RegisterRequest request, string property) =>
        _validator.Validate(request).Errors
            .Where(error => error.PropertyName == property)
            .Select(error => error.ErrorMessage)
            .ToArray();
}
