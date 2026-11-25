namespace PlatformService.Application.Common.Exceptions;

/// <summary>The fields of a registration that can be taken.</summary>
[Flags]
public enum TakenAccountFields
{
    None = 0,
    UserName = 1,
    Email = 2,
}
