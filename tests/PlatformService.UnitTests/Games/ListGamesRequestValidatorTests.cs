using PlatformService.Application.Games;

namespace PlatformService.UnitTests.Games;

public sealed class ListGamesRequestValidatorTests
{
    private readonly ListGamesRequestValidator _validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 20)]
    public void WithinTheLimitsOfTheContract_IsValid(int page, int pageSize)
    {
        Assert.True(_validator.Validate(new ListGamesRequest(page, pageSize)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_BelowOne_HasItsMessage(int page)
    {
        var result = _validator.Validate(new ListGamesRequest(page, ListGamesRequest.DefaultPageSize));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ListGamesRequest.Page), error.PropertyName);
        Assert.Equal(ListGamesRequestValidator.PageTooSmall, error.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_OutOfItsRange_HasItsMessage(int pageSize)
    {
        var result = _validator.Validate(new ListGamesRequest(ListGamesRequest.DefaultPage, pageSize));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ListGamesRequest.PageSize), error.PropertyName);
        Assert.Equal(ListGamesRequestValidator.PageSizeOutOfRange, error.ErrorMessage);
    }
}
