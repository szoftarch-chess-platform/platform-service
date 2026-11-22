using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformService.Infrastructure.Identity;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Completes the model that Identity gives to <see cref="PlatformUser"/>.
/// </summary>
/// <remarks>
/// Identity names its tables and indexes explicitly, and the snake case convention of PS-ADR-7 renames only
/// the names that it derives itself. So the names are set here.
/// </remarks>
internal sealed class PlatformUserConfiguration : IEntityTypeConfiguration<PlatformUser>
{
    public void Configure(EntityTypeBuilder<PlatformUser> builder)
    {
        builder.ToTable("users");

        builder.HasIndex(user => user.NormalizedUserName)
            .HasDatabaseName("ix_users_normalized_user_name");

        // Identity checks the email address before it saves, but its index is not unique. The unique index
        // keeps two parallel registrations from taking the same address regardless of case (ADR-4).
        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_users_normalized_email");

        // The convention names these foreign keys before the table gets its name above.
        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(claim => claim.UserId)
            .HasConstraintName("fk_user_claims_users_user_id");
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(login => login.UserId)
            .HasConstraintName("fk_user_logins_users_user_id");
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(token => token.UserId)
            .HasConstraintName("fk_user_tokens_users_user_id");
    }
}
