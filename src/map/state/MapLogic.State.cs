
namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public partial class MapLogic
{
    [Meta]
    public abstract partial record State : StateLogic<State>;
}
