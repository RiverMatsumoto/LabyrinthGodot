namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface IMapMovementLogic : ILogicBlock<MapMovementLogic.State>;

[Meta, Id("map_movement_logic")]
[LogicBlock(typeof(State), Diagram = true)]
public partial class MapMovementLogic : LogicBlock<MapMovementLogic.State>, IMapMovementLogic
{
    public override Transition GetInitialState() => To<State.Idle>();
}
