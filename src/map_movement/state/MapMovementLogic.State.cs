namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public partial class MapMovementLogic
{
    [Meta]
    public abstract partial record State : StateLogic<State>;
}
