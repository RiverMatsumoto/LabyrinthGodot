namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;

[Meta(typeof(IAutoNode))]
public partial class SkillMenu : MenuSubmenu<MenuHubLogicState.SkillMenu>
{
    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
