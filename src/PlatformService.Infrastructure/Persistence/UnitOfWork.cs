using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>The commit of a use case on <see cref="PlatformDbContext"/>, the Unit of Work of PS-ADR-7.</summary>
internal sealed class UnitOfWork(PlatformDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The loaded entities are out of date now, so the next query of the use case loads them again.
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyConflictException("A parallel request changed the same data first.", exception);
        }
        catch (DbUpdateException exception) when (TakenAccountField(exception) is { } field)
        {
            throw new AccountTakenException(field, exception);
        }
        catch (DbUpdateException exception) when (IsRecordedGame(exception))
        {
            // The game stays unsaved, so it is forgotten like the loaded entities after a conflict.
            dbContext.ChangeTracker.Clear();
            throw new GameAlreadyRecordedException("A parallel delivery stored the same game first.", exception);
        }
    }

    /// <summary>Two deliveries of the same game both pass the check of the use case, and the database refuses the second one.</summary>
    private static bool IsRecordedGame(DbUpdateException exception) =>
        ViolatedUniqueConstraint(exception) is GameConfiguration.PrimaryKey or GameConfiguration.EventIdIndex;

    /// <summary>
    /// Two registrations at the same time both pass the check of Identity, and the unique index refuses the second
    /// one. PostgreSQL reports only the first index that the row violates.
    /// </summary>
    private static TakenAccountFields? TakenAccountField(DbUpdateException exception) =>
        ViolatedUniqueConstraint(exception) switch
        {
            PlatformUserConfiguration.UserNameIndex => TakenAccountFields.UserName,
            PlatformUserConfiguration.EmailIndex => TakenAccountFields.Email,
            _ => null,
        };

    /// <summary>The primary key or the unique index that the commit violated, or <see langword="null"/> for another failure.</summary>
    private static string? ViolatedUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            ? postgresException.ConstraintName
            : null;
}
