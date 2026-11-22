namespace PlatformService.Application.Abstractions;

/// <summary>A token with its lifetime, which becomes the Max-Age of its cookie.</summary>
/// <remarks>A class rather than a record, so that <see cref="object.ToString"/> does not print the token.</remarks>
public sealed class IssuedToken(string value, TimeSpan lifetime)
{
    public string Value { get; } = value;

    public TimeSpan Lifetime { get; } = lifetime;
}
