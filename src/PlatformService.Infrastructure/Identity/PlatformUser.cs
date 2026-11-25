using Microsoft.AspNetCore.Identity;

namespace PlatformService.Infrastructure.Identity;

/// <summary>
/// The user type of ASP.NET Core Identity. It stays in the Infrastructure, as PS-ADR-11 decides.
/// </summary>
/// <remarks>
/// The key is the UUID v7 of ADR-4, which the service assigns at registration. Business data of the users, such as
/// a rating, does not go here without the decision that PS-ADR-11 asks for.
/// </remarks>
internal sealed class PlatformUser : IdentityUser<Guid>;
