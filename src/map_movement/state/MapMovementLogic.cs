namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IMapMovementLogic : ILogicBlock;

[Meta, Id("map_movement_logic")]
// [LogicBlock(typeof(State), Diagram = true)]
public partial class MapMovementLogic : LogicBlock, IMapMovementLogic
{
}
