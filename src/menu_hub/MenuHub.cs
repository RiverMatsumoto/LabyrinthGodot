namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class MenuHub : CanvasLayer
{
    public override void _Notification(int what) => this.Notify(what);

}
