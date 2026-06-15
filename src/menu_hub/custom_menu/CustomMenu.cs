namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class CustomMenu : MenuSubmenu<MenuHubLogicState.CustomMenu>
{
    [Node("CenterContainer/Content/BackButton")]
    protected override Button BackButton { get; set; } = default!;

    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
