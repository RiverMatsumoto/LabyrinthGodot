namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class PartyMenu : MenuSubmenu<MenuHubLogicState.PartyMenu>
{
    [Node("CenterContainer/Content/BackButton")]
    protected override Button BackButton { get; set; } = default!;

    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
