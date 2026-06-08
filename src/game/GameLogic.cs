namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IGameLogic : ILogicBlock;

[Meta]
public partial class GameLogic : AutoBlock, IGameLogic
{
    public GameLogic()
    {
        Preallocate<GameLogicState>();
    }
}
