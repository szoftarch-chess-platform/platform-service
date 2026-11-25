using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.IntegrationTests.ErrorHandling;

/// <summary>
/// Exists only in the test project. <see cref="PlatformApiFactory"/> adds it to the app as an ApplicationPart.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/test/errors")]
public sealed class ErrorTestController : ControllerBase
{
    public const string UnhandledMessage = "Internal failure that must not reach the client.";

    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() => throw new NotFoundException("The game does not exist.");

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() => throw new ConflictException("The user name is already taken.");

    [HttpGet("unauthorized")]
    public IActionResult ThrowUnauthorized() => throw new UnauthorizedException("The user name or the password is wrong.");

    [HttpGet("forbidden")]
    public IActionResult ThrowForbidden() => throw new ForbiddenException("You are not a player of this game.");

    [HttpGet("validation")]
    public IActionResult ThrowValidation() => throw new ValidationException(
    [
        new ValidationFailure("UserName", "User name is required."),
        new ValidationFailure("UserName", "User name is too short."),
        new ValidationFailure("Address.PostalCode", "Postal code is invalid."),
    ]);

    [HttpGet("unhandled")]
    public IActionResult ThrowUnhandled() => throw new InvalidOperationException(UnhandledMessage);

    /// <summary>Validates the input the way a use case does, so the validator runs first and throws.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        TestRequest request,
        [FromServices] IValidator<TestRequest> validator,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return NoContent();
    }
}

/// <summary>Has no DataAnnotations, so MVC must not add an implicit Required check for the non-nullable <see cref="UserName"/>.</summary>
public sealed record TestRequest(string UserName);

public sealed class TestRequestValidator : AbstractValidator<TestRequest>
{
    public const string UserNameRequired = "User name is required.";

    public TestRequestValidator()
    {
        RuleFor(request => request.UserName).NotEmpty().WithMessage(UserNameRequired);
    }
}
