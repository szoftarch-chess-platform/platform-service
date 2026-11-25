using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Gives the tokens table of Identity a snake case name, for the reason in <see cref="PlatformUserConfiguration"/>.
/// The service does not use the table, but the stores of Identity need it in the model.
/// </summary>
internal sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");
}
