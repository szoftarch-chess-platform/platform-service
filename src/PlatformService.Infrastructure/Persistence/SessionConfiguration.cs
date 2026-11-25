using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformService.Domain.Sessions;
using PlatformService.Infrastructure.Identity;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The <c>sessions</c> table of PS-ADR-12. The Domain sets its properties only in the constructor, so they are
/// mapped explicitly.
/// </summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        // The Domain creates the ids, as for the refresh tokens.
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.StartedAt);
        builder.Property(session => session.RevokedAt);

        builder.HasOne<PlatformUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The tokens belong to the aggregate, and the foreign key stays out of the Domain.
        builder.HasMany(session => session.RefreshTokens)
            .WithOne()
            .HasForeignKey("SessionId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
