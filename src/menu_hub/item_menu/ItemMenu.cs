namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IItemMenu : IControl
{

}

[Meta(typeof(IAutoNode))]
public partial class ItemMenu : Control, IItemMenu
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    private IMenuHubLogic MenuHubLogic => this.DependOn<IMenuHubLogic>();

    public void OnResolved()
    {

    }
}
