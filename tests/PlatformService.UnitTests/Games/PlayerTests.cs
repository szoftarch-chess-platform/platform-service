using PlatformService.Domain.Games;

namespace PlatformService.UnitTests.Games;

public sealed class PlayerTests
{
    private static readonly Guid _userId = Guid.CreateVersion7();

    [Fact]
    public void Create_KeepsTheUserIdOfAUser()
    {
        var player = Player.Create(Player.UserKind, _userId, "alice");

        Assert.Equal(_userId, player.UserId);
    }

    // A bot never puts a game into the history of a user, and neither does a kind of a later contract.
    [Theory]
    [InlineData(Player.BotKind)]
    [InlineData("guest")]
    public void Create_DropsTheUserIdOfAPlayerThatIsNotAUser(string kind)
    {
        var player = Player.Create(kind, _userId, "someone");

        Assert.Null(player.UserId);
        Assert.Equal(kind, player.Kind);
    }
}
