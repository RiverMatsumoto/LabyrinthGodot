namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IMapMovementCamera : ICamera3D
{

}

[Meta(typeof(IAutoNode))]
public partial class MapMovementCamera : Camera3D
{
    public override void _Notification(int what) => this.Notify(what);

}
