namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;

[Meta(typeof(IAutoNode))]
public partial class CustomMenu : MenuSubmenu<MenuHubLogicState.CustomMenu>
{
    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
