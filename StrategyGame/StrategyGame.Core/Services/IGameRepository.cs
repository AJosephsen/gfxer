using StrategyGame.Core.Models.Game;

namespace StrategyGame.Core.Services;

public interface IGameRepository
{
    void Save(GameState game);
    GameState Load(string gameId);
    List<GameSummary> ListAll();
}
