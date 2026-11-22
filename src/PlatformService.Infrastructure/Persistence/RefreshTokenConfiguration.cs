using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformService.Domain.Sessions;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The <c>refresh_tokens</c> table of PS-ADR-12. The Domain sets its properties only in the constructor, so they
/// are mapped explicitly.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        // The Domain creates the ids. With a generated key, EF Core would take a new token with an id in a loaded
        // session for an existing one, and it would update the token instead of inserting it.
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.IssuedAt);
        builder.Property(token => token.ExpiresAt);

        // A refresh is found by the hash of its token.
        builder.Property(token => token.TokenHash);
        builder.HasIndex(token => token.TokenHash).IsUnique();
    }
}
