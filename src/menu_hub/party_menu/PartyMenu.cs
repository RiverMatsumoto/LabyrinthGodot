namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;

[Meta(typeof(IAutoNode))]
public partial class PartyMenu : MenuSubmenu<MenuHubLogicState.PartyMenu>
{
    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
