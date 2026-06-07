namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IMapMovementLogic : ILogicBlock;

[Meta]
public partial class MapMovementLogic : LogicBlock, IMapMovementLogic
{

}
