namespace PlatformService.Application.Abstractions;

/// <summary>What the use cases know about an account of <see cref="IUserAccounts"/>.</summary>
public sealed record UserAccount(Guid Id, string UserName);
