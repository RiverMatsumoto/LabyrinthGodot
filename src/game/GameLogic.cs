namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IGameLogic : ILogicBlock;

[Meta]
public partial class GameLogic : LogicBlock, IGameLogic
{
    public GameLogic()
    {
        Set(new GameState.MainMenu());
        Set(new GameState.InGame());
    }
}
