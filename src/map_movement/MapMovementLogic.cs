namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IMapMovementLogic : ILogicBlock;

[Meta]
public partial class MapMovementLogic : AutoBlock, IMapMovementLogic
{
    public MapMovementLogic()
    {
        Preallocate<MapMovementLogicState>();
    }
}
