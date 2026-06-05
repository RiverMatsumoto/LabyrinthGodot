namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IGameLogic : ILogicBlock;

[Meta, Id("game_logic")]
public partial class GameLogic : LogicBlock, IGameLogic
{
    public GameLogic()
    {
        Set(new GameState.MainMenu());
        Set(new GameState.InGame());
    }
}
