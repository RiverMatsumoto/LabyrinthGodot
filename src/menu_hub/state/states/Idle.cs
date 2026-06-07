namespace Labyrinth;

using Chickensoft.LogicBlocks;

public partial record MenuHubLogicState
{
    public record Idle : MenuHubLogicState
    {
        public Idle()
        {

        }
    }
}