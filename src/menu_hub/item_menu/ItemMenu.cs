namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IItemMenu : IControl;

[Meta(typeof(IAutoNode))]
public partial class ItemMenu :
    MenuSubmenu<MenuHubLogicState.ItemMenu>,
    IItemMenu
{
    [Node("CenterContainer/Content/BackButton")]
    protected override Button BackButton { get; set; } = default!;

    [Dependency]
    protected override IMenuHubLogic MenuHubLogic =>
        this.DependOn<IMenuHubLogic>();
}
