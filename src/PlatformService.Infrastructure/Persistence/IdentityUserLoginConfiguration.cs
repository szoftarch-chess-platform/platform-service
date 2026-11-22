using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Gives the logins table of Identity a snake case name, for the reason in <see cref="PlatformUserConfiguration"/>.
/// The service does not use the table, but the stores of Identity need it in the model.
/// </summary>
internal sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");
}
