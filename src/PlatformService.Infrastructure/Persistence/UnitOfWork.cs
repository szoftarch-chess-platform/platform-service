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
        catch (DbUpdateException exception) when (TakenAccountField(exception) is { } field)
        {
            throw new AccountTakenException(field, exception);
        }
    }

    /// <summary>
    /// Two registrations at the same time both pass the check of Identity, and the unique index refuses the second
    /// one. PostgreSQL reports only the first index that the row violates.
    /// </summary>
    private static TakenAccountFields? TakenAccountField(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            ? postgresException.ConstraintName switch
            {
                PlatformUserConfiguration.UserNameIndex => TakenAccountFields.UserName,
                PlatformUserConfiguration.EmailIndex => TakenAccountFields.Email,
                _ => null,
            }
            : null;
}
