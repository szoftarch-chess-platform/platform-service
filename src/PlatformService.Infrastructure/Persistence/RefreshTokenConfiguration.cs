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

        // The cleanup deletes the expired tokens by this index.
        builder.Property(token => token.ExpiresAt);
        builder.HasIndex(token => token.ExpiresAt);

        // The update of a replacement has the condition replaced_at IS NULL, so of two parallel refreshes with the
        // same token only one replaces it, and the other gets ConcurrencyConflictException (PS-ADR-12).
        builder.Property(token => token.ReplacedAt).IsConcurrencyToken();

        // A refresh is found by the hash of its token.
        builder.Property(token => token.TokenHash);
        builder.HasIndex(token => token.TokenHash).IsUnique();
    }
}
