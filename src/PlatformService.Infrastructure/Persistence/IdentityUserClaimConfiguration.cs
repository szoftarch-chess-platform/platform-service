using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Gives the claims table of Identity a snake case name, for the reason in <see cref="PlatformUserConfiguration"/>.
/// The service does not use the table, but the stores of Identity need it in the model.
/// </summary>
internal sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");
}
