namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IMapLogic : ILogicBlock;

[Meta]
public partial class MapLogic : AutoBlock, IMapLogic
{
    public MapLogic()
    {
        Preallocate<MapLogicState>();
    }
}
