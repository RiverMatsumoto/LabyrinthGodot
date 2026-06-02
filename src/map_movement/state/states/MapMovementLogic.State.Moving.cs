namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public partial record MapMovementState
{
    public record Moving : MapMovementState
    {
        public Moving()
        {
            // start lerping character in on enter with the params passed in?
        }
    }
}
