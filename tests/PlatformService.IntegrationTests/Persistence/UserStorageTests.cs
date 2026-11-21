using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlatformService.Infrastructure.Identity;
using PlatformService.Infrastructure.Persistence;

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

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddUserAsync(CreateUser(Guid.CreateVersion7(), userName.ToUpperInvariant())));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    /// <summary>The database refuses the address even without the check of Identity, as in two parallel registrations.</summary>
    [Fact]
    public async Task EmailAddresses_AreUniqueRegardlessOfCase()
    {
        var email = $"{NewUserName()}@example.com";
        await AddUserAsync(CreateUser(Guid.CreateVersion7(), NewUserName(), email));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddUserAsync(CreateUser(Guid.CreateVersion7(), NewUserName(), email.ToUpperInvariant())));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
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

    /// <summary>A user name of 13 characters that matches the rules of ADR-4 and no other test.</summary>
    private static string NewUserName() => $"u{Guid.NewGuid():N}"[..13];

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

    private async Task AddUserAsync(PlatformUser user)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IdentityResult> CreateWithUserManagerAsync(string userName, string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { Id = Guid.CreateVersion7(), UserName = userName, Email = email };
        return await userManager.CreateAsync(user, password);
    }
}
