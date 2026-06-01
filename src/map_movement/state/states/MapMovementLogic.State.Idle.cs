namespace Labyrinth;

using Chickensoft.Introspection;

public partial class MapMovementLogic
{
    public partial record State
    {
        [Meta]
        public partial record Idle : State
        {
            // public Transition
        }
    }
}
