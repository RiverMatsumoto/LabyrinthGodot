namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public partial record MapMovementLogicState
{
    public record Moving : MapMovementLogicState
    {
        public Moving()
        {
            // start lerping character in on enter with the params passed in?
        }
    }
}
