namespace StrategyCards.Core.Tests;

public class GameTests
{
    [Fact]
    public void Constructor_SetsNameAndGeneratesId()
    {
        var game = new Game("NameOfGame");

        Assert.Equal("NameOfGame", game.Name);
        Assert.NotEqual(Guid.Empty, game.Id);
    }
}
