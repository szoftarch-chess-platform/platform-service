using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PlatformService.IntegrationTests.Json;

/// <summary>
/// Exists only in the test project, like the ErrorTestController. It answers with an enum and a property without a value.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/test/json")]
public sealed class JsonTestController : ControllerBase
{
    [HttpGet]
    public JsonTestBody Get() => new(JsonTestReason.ThreefoldRepetition, Note: null);

    /// <summary>Sends the body back, so a test can see how an enum is read.</summary>
    [HttpPost]
    public JsonTestBody Echo(JsonTestBody body) => body;
}

public enum JsonTestReason
{
    Checkmate,
    ThreefoldRepetition,
}

public sealed record JsonTestBody(JsonTestReason Reason, string? Note);
