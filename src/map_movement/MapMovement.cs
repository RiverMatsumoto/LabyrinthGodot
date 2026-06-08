namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.Sync.Primitives;
using Godot;

// Interfaces for visuals specific
public interface IMapMovement : INode3D
{
    MapEntityId EntityId { get; }
    Vector2I GridPosition { get; }
    void Initialize(MapEntityId id, Vector2I startPosition);
}

[Meta(typeof(IAutoNode))]
public partial class MapMovement : Node3D, IMapMovement
{
    public override void _Notification(int what) => this.Notify(what);

    private bool _isInitialized;
    private bool _isMoving;
    private Tween? _moveTween;
    private Vector2I _gridPosition;

    public IMapMovementLogic MapMovementLogic { get; set; } = default!;
    public MapEntityId EntityId { get; private set; }
    public Vector2I GridPosition => _gridPosition;

    [Dependency]
    public IGridMap GridMap => this.DependOn<IGridMap>();

    [Export]
    public string EntityIdValue { get; set; } = string.Empty;

    [Export]
    public Vector2I InitialGridPosition { get; set; }

    [Export]
    public float MoveDuration { get; set; } = 0.18f;

    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();
    public AutoChannel.Binding MapRepoBinding { get; set; } = default!;

    public void Setup()
    {
        MapMovementLogic = new MapMovementLogic();
    }

    public void OnResolved()
    {
        MapMovementLogic.Start<MapMovementLogicState.Idle>();
        MapRepoBinding = MapRepo.AutoChannel.Bind();
        MapRepoBinding.On((in IMapRepo.MapEntityWasUnregistered message) =>
            QueueFree());
        GlobalPosition = GridToGlobalPosition(_gridPosition);
    }

    public void Initialize(MapEntityId id, Vector2I startPosition)
    {
        if (_isInitialized || id.IsEmpty)
            GD.PrintErr("MapMovement: initialization error");

        EntityId = id;
        _gridPosition = startPosition;
        _isInitialized = true;
        Name = id.Value;
    }

    public bool TryRequestMove(Vector2I offset)
    {
        if (!_isInitialized || _isMoving)
        {
            return false;
        }

        if (!MapRepo.TryMoveEntity(EntityId, offset, out var move))
        {
            MapMovementLogic.Input(
                new MapMovementLogicState.Input.MoveBlocked(offset)
            );
            return false;
        }

        _isMoving = true;
        _gridPosition = move.To;

        MapMovementLogic.Input(
            new MapMovementLogicState.Input.MoveAccepted(move)
        );
        AnimateMove(move);
        return true;
    }

    public static Vector3I GridToMapCell(Vector2I gridPosition) =>
        new(gridPosition.X, 0, gridPosition.Y);

    public static Vector3 GridToWorldPosition(Vector2I gridPosition) =>
        new(gridPosition.X, 0, gridPosition.Y);

    private void AnimateMove(GridMove move)
    {
        var target = GridToGlobalPosition(move.To);

        _moveTween?.Kill();

        if (MoveDuration <= 0)
        {
            Position = target;
            FinishMove();
            return;
        }

        _moveTween = CreateTween();
        _moveTween.TweenProperty(this, "position", target, MoveDuration);
        _moveTween.Finished += FinishMove;
    }

    private Vector3 GridToGlobalPosition(Vector2I gridPosition) =>
        ToGlobal(GridMap.MapToLocal(GridToMapCell(gridPosition)));

    private void FinishMove()
    {
        _moveTween = null;
        _isMoving = false;

        MapMovementLogic.Input(new MapMovementLogicState.Input.Arrived());
    }

    public void OnExitTree()
    {
        _moveTween?.Kill();
        _moveTween = null;

        MapRepoBinding.Dispose();
        MapMovementLogic.Dispose();
    }
}
