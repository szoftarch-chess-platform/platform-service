using FluentValidation;

namespace PlatformService.Application.Games;

/// <summary>The limits of the query of <c>GET /api/games</c> in the contract.</summary>
internal sealed class ListGamesRequestValidator : AbstractValidator<ListGamesRequest>
{
    public const string PageTooSmall = "The page must be at least 1.";
    public const string PageSizeOutOfRange = "The page size must be 1 to 100.";

    public ListGamesRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage(PageTooSmall);

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, ListGamesRequest.MaxPageSize)
            .WithMessage(PageSizeOutOfRange);
    }
}
