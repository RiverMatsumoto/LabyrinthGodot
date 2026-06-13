namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record MapMovementLogicState : LogicBlockState
{
    protected MapMovementLogic.Data Data => Get<MapMovementLogic.Data>();
}
