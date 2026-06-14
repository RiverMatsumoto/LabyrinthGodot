namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;

[Meta(typeof(IAutoNode))]
public partial class StatusMenu : MenuSubmenu<MenuHubLogicState.StatusMenu>
{
    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
