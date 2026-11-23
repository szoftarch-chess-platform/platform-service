using FluentValidation;

namespace PlatformService.Application.Games;

/// <summary>The limits of the query of <c>GET /api/games</c> in the contract.</summary>
/// <remarks>The messages take their numbers from the limits of the rules, so a changed limit changes them too.</remarks>
internal sealed class ListGamesRequestValidator : AbstractValidator<ListGamesRequest>
{
    public ListGamesRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("The page must be at least {ComparisonValue}.");

        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, ListGamesRequest.MaxPageSize)
            .WithMessage("The page size must be {From} to {To}.");
    }
}
