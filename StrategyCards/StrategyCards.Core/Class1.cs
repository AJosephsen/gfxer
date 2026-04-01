namespace StrategyCards.Core;

public class Game
{
	public Guid Id { get; }

	public string Name { get; }

	public Game(string name)
	{
		Name = name;
		Id = Guid.NewGuid();
	}
}
