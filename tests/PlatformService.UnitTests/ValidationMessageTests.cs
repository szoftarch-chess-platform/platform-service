using System.Globalization;
using FluentValidation;
using PlatformService.Application;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests;

public sealed class ValidationMessageTests
{
    [Fact]
    public void AddApplication_MakesValidationMessagesEnglishInAnyCulture()
    {
        new FakeServiceCollection().AddApplication();
        var validator = new InlineValidator<Sample>();
        validator.RuleFor(sample => sample.Name).NotEmpty();

        var originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("hu-HU");
        try
        {
            var result = validator.Validate(new Sample(string.Empty));

            Assert.Equal("'Name' must not be empty.", Assert.Single(result.Errors).ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    public sealed record Sample(string Name);
}
