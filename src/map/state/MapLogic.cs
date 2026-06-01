namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, LogicBlock(typeof(State), Diagram = true)]
public partial class MapLogic : LogicBlock<MapLogic.State>
{
    public override Transition GetInitialState() => To<State>();

    public abstract partial record State : StateLogic<State>;
}
