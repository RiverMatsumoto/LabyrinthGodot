namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IEnemyMapMovementController : INode
{
    void RequestMove(Vector2I direction);
}

[Meta(typeof(IAutoNode))]
public partial class EnemyMapMovementController
    : Node,
        IEnemyMapMovementController
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    public IMapMovementLogic MapMovementLogic =>
        this.DependOn<IMapMovementLogic>();

    public void RequestMove(Vector2I direction) =>
        MapMovementLogic.RequestMove(direction);
}
