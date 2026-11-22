using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlatformService.Application.Abstractions;
using PlatformService.Infrastructure.Identity;
using PlatformService.Infrastructure.Persistence;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Persistence;

/// <summary>
/// The storage of the accounts in PostgreSQL. The tests share the database, so every user gets a new name.
/// </summary>
public sealed class UserStorageTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task Model_HasNoChangesWithoutAMigration()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    /// <summary>
    /// The ids of a UUID v7 from <see cref="Guid.CreateVersion7(DateTimeOffset)"/> are sorted by time in the
    /// uuid column too. The offsets change the bytes whose order differs between .NET and RFC 9562.
    /// </summary>
    [Fact]
    public async Task UuidV7Ids_AreStoredInTimeOrder()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TimeSpan[] offsets =
        [
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(60),
            TimeSpan.FromDays(400),
        ];
        var ids = offsets.Select(offset => Guid.CreateVersion7(start + offset)).ToArray();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

            // The newest first, so that the order of the inserts cannot explain the result.
            db.Users.AddRange(ids.AsEnumerable().Reverse().Select(id => CreateUser(id, NewUserName())));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var stored = await db.Users
                .Where(user => ids.Contains(user.Id))
                .OrderBy(user => user.Id)
                .Select(user => user.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ids, stored);
        }
    }

    [Fact]
    public async Task UserNames_AreUniqueRegardlessOfCase()
    {
        var userName = NewUserName();
        await AddUserAsync(CreateUser(Guid.CreateVersion7(), userName));

        // Another email address, so that only the index of the user name can refuse the user.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddUserAsync(CreateUser(Guid.CreateVersion7(), userName.ToUpperInvariant(), $"{NewUserName()}@example.com")));

        AssertUniqueViolation(exception, "ix_users_normalized_user_name");
    }

    /// <summary>The database refuses the address even without the check of Identity, as in two parallel registrations.</summary>
    [Fact]
    public async Task EmailAddresses_AreUniqueRegardlessOfCase()
    {
        var email = $"{NewUserName()}@example.com";
        await AddUserAsync(CreateUser(Guid.CreateVersion7(), NewUserName(), email));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddUserAsync(CreateUser(Guid.CreateVersion7(), NewUserName(), email.ToUpperInvariant())));

        AssertUniqueViolation(exception, "ix_users_normalized_email");
    }

    [Fact]
    public async Task UserManager_AcceptsAPasswordOf8CharactersWithoutCompositionRules()
    {
        var result = await CreateWithUserManagerAsync($"{NewUserName()}_x-1", $"{NewUserName()}@example.com", "abcdefgh");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("al.ice", "correct horse battery", nameof(IdentityErrorDescriber.InvalidUserName))]
    [InlineData("al@ice", "correct horse battery", nameof(IdentityErrorDescriber.InvalidUserName))]
    [InlineData(null, "1234567", nameof(IdentityErrorDescriber.PasswordTooShort))]
    public async Task UserManager_RejectsWhatTheRulesOfAdr4Forbid(string? userName, string password, string errorCode)
    {
        var result = await CreateWithUserManagerAsync(userName ?? NewUserName(), $"{NewUserName()}@example.com", password);

        Assert.Contains(errorCode, result.Errors.Select(error => error.Code));
    }

    [Fact]
    public async Task UserManager_RejectsATakenEmailAddressRegardlessOfCase()
    {
        var email = $"{NewUserName()}@example.com";
        Assert.True((await CreateWithUserManagerAsync(NewUserName(), email, "correct horse battery")).Succeeded);

        var result = await CreateWithUserManagerAsync(NewUserName(), email.ToUpperInvariant(), "correct horse battery");

        Assert.Contains(nameof(IdentityErrorDescriber.DuplicateEmail), result.Errors.Select(error => error.Code));
    }

    /// <summary>The store of Identity leaves the save to the commit of the use case (PS-ADR-11).</summary>
    [Fact]
    public async Task UserManager_DoesNotSaveTheUserWithoutTheCommit()
    {
        var userName = NewUserName();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = new PlatformUser { Id = Guid.CreateVersion7(), UserName = userName, Email = $"{userName}@example.com" };
            Assert.True((await userManager.CreateAsync(user, "correct horse battery")).Succeeded);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            Assert.Null(await userManager.FindByNameAsync(userName));
        }
    }

    private static PlatformUser CreateUser(Guid id, string userName, string? email = null)
    {
        email ??= $"{userName}@example.com";
        return new PlatformUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
        };
    }

    /// <summary>A user that violates both unique indexes is refused by only one, so the name of the index is checked.</summary>
    private static void AssertUniqueViolation(DbUpdateException exception, string indexName)
    {
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal(indexName, postgresException.ConstraintName);
    }

    private async Task AddUserAsync(PlatformUser user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Creates the user with Identity and commits it, as a use case does.</summary>
    private async Task<IdentityResult> CreateWithUserManagerAsync(string userName, string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { Id = Guid.CreateVersion7(), UserName = userName, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return result;
    }
}
