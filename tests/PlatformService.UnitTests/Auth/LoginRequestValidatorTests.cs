using PlatformService.Application.Auth;

namespace PlatformService.UnitTests.Auth;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void AValidRequest_HasNoErrors()
    {
        Assert.True(_validator.Validate(Request()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void UserName_Missing_IsRequired(string? userName)
    {
        Assert.Equal([AccountRules.UserNameRequired], Messages(Request(userName: userName), nameof(LoginRequest.UserName)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Password_Missing_IsRequired(string? password)
    {
        Assert.Equal([AccountRules.PasswordRequired], Messages(Request(password: password), nameof(LoginRequest.Password)));
    }

    [Fact]
    public void Password_LongerThanAtRegistration_IsRefused()
    {
        Assert.Equal(
            [AccountRules.PasswordTooLong],
            Messages(Request(password: new string('p', 129)), nameof(LoginRequest.Password)));
    }

    [Fact]
    public void Password_CountsCodePoints()
    {
        // 128 code points, which are 256 UTF-16 units.
        var longest = string.Concat(Enumerable.Repeat("\U0001F600", 128));

        Assert.Empty(Messages(Request(password: longest), nameof(LoginRequest.Password)));
    }

    private static LoginRequest Request(string? userName = "alice", string? password = "correct horse battery") =>
        new() { UserName = userName!, Password = password! };

    private string[] Messages(LoginRequest request, string property) =>
        _validator.Validate(request).Errors
            .Where(error => error.PropertyName == property)
            .Select(error => error.ErrorMessage)
            .ToArray();
}
