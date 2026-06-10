namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IEnemyMapEntity : INode3D
{
    MapEntityId EntityId { get; }
    IMapMovement MapMovement { get; }
    IEnemyMapMovementController MovementController { get; }

    void Initialize(MapEntityId id, Vector2I startPosition);
}

[Meta(typeof(IAutoNode))]
public partial class EnemyMapEntity : Node3D, IEnemyMapEntity
{
    public override void _Notification(int what) => this.Notify(what);

    private bool _isInitialized;
    private MapEntityId _entityId;
    private Vector2I _startPosition;

    [Node] public IMapMovement MapMovement { get; private set; } = default!;
    [Node]
    public IEnemyMapMovementController MovementController
    {
        get;
        private set;
    } = default!;

    public MapEntityId EntityId => _entityId;

    public void Initialize(MapEntityId id, Vector2I startPosition)
    {
        if (_isInitialized || id.IsEmpty)
        {
            GD.PrintErr("EnemyMapEntity: initialization error");
            return;
        }

        _entityId = id;
        _startPosition = startPosition;
        _isInitialized = true;
        Name = id.Value;
    }

    public void OnResolved()
    {
        if (!_isInitialized)
        {
            GD.PrintErr("EnemyMapEntity: resolved before initialization");
            return;
        }

        MapMovement.Initialize(_entityId, _startPosition);
    }
}
