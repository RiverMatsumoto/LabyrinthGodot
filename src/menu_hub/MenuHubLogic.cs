namespace Labyrinth;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;

public interface IMenuHubLogic : ILogicBlock;

[Meta]
public partial class MenuHubLogic : AutoBlock, IMenuHubLogic
{
    public MenuHubLogic()
    {
        Preallocate<MenuHubLogicState>();
    }
}
