namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;

[Meta(typeof(IAutoNode))]
public partial class EquipMenu : MenuSubmenu<MenuHubLogicState.EquipMenu>
{
    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
