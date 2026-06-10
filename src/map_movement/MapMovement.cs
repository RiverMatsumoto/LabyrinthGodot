namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.Sync.Primitives;
using Godot;

public interface IMapMovement : INode3D,
    IProvide<IMapMovementLogic>,
    IProvide<IMapMovement>
{
    MapEntityId EntityId { get; }
    IMapMovementLogic MapMovementLogic { get; }
    bool IsEnabled { get; }
    bool IsMoving { get; }

    void StartCooldownTimer();
    void Initialize(MapEntityId id, Vector2I startPosition);
}

[Meta(typeof(IAutoNode))]
public partial class MapMovement : Node3D, IMapMovement
{
    public override void _Notification(int what) => this.Notify(what);

    private bool _isInitialized;
    private Tween? _moveTween;
    private Tween? _turnTween;
    private Vector2I _startPosition;

    public IMapMovementLogic MapMovementLogic { get; } = new MapMovementLogic();
    IMapMovementLogic IProvide<IMapMovementLogic>.Value() => MapMovementLogic;
    private MapMovementLogic.Binding? _mapMovementBinding;

    IMapMovement IProvide<IMapMovement>.Value() => this;

    public MapEntityId EntityId => MapMovementLogic.Data.EntityId;
    public bool IsEnabled =>
        MapMovementLogic.State is not MapMovementLogicState.Disabled;
    public bool IsMoving =>
        MapMovementLogic.State is MapMovementLogicState.Moving;

    [Dependency]
    public IGridMap GridMap => this.DependOn<IGridMap>();

    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();
    private AutoChannel.Binding? _mapRepoBinding;
    private AutoValue<double>.Binding? _mapMoveDurationBinding;

    private PackedScene _playerCameraScene =
        GD.Load<PackedScene>("res://src/map_movement/PlayerCamera.tscn");
    private PackedScene _playerMovementController =
        GD.Load<PackedScene>("res://src/map_movement/PlayerMovementController.tscn");

    [Node]
    public Timer CooldownTimer { get; set; } = default!;

    public void Setup()
    {
    }

    public void OnResolved()
    {
        MapMovementLogic.Set(MapRepo);
        MapMovementLogic.Set(this as IMapMovement);

        _mapMoveDurationBinding = GameRepo.MapMoveDuration.Bind()
            .OnValue(moveDuration =>
                MapMovementLogic.Data.MoveDuration = moveDuration);

        _mapMovementBinding = MapMovementLogic.Bind()
            .OnOutput((in MapMovementLogicState.Output.MoveStarted output) =>
                AnimateMove(output.Move))
            .OnOutput((in MapMovementLogicState.Output.TurnStarted output) =>
                AnimateTurn(output.FacingDirection));

        _mapRepoBinding = MapRepo.AutoChannel.Bind();
        _mapRepoBinding.On((in IMapRepo.MapEntityWasUnregistered message) =>
        {
            if (message.Id == EntityId)
            {
                QueueFree();
            }
        });

        ApplyStartPosition();

        this.Provide();

        MapMovementLogic.Start<MapMovementLogicState.Idle>();
    }

    public void Initialize(MapEntityId id, Vector2I startPosition)
    {
        if (_isInitialized || id.IsEmpty)
        {
            GD.PrintErr("MapMovement: initialization error");
            return;
        }

        var data = MapMovementLogic.Data;
        data.EntityId = id;

        _startPosition = startPosition;
        _isInitialized = true;
        Name = id.Value;

        if (id.Value == "player")
        {
            SpawnPlayerCamera();
            SpawnPlayerMovementController();
        }
    }

    public static Vector3I GridToMapCell(Vector2I gridPosition) =>
        new(gridPosition.X, 0, gridPosition.Y);

    public static Vector3 GridToWorldPosition(Vector2I gridPosition) =>
        new(gridPosition.X, 0, gridPosition.Y);

    public static float FacingDirectionToYaw(Vector2I facingDirection)
    {
        if (facingDirection == GridDirection.North)
        {
            return 0.0f;
        }

        if (facingDirection == GridDirection.East)
        {
            return -Mathf.Pi / 2.0f;
        }

        if (facingDirection == GridDirection.South)
        {
            return Mathf.Pi;
        }

        if (facingDirection == GridDirection.West)
        {
            return Mathf.Pi / 2.0f;
        }

        throw new ArgumentOutOfRangeException(
            nameof(facingDirection),
            facingDirection,
            "Facing direction must be a cardinal unit vector."
        );
    }

    private void AnimateMove(GridMove move)
    {
        var target = GridToGlobalPosition(move.To);

        _moveTween?.Kill();

        var moveDuration = MapMovementLogic.Data.MoveDuration;
        if (moveDuration <= 0)
        {
            GlobalPosition = target;
            FinishMove();
            return;
        }

        _moveTween = CreateTween();
        _moveTween.TweenProperty(
            this,
            "global_position",
            target + new Vector3(0, 0.5f * GridMap.Scale.Y, 0),
            moveDuration
        );
        _moveTween.Finished += FinishMove;
    }

    private Vector3 GridToGlobalPosition(Vector2I gridPosition) =>
        GridMap.ToGlobal(GridMap.MapToLocal(GridToMapCell(gridPosition)));

    private void ApplyStartPosition()
    {
        if (!_isInitialized)
        {
            return;
        }

        GlobalPosition = GridToGlobalPosition(_startPosition);

        if (MapRepo.TryGetEntityPose(EntityId, out var pose))
        {
            Rotation = Rotation with
            {
                Y = FacingDirectionToYaw(pose.FacingDirection),
            };
        }
    }

    private void FinishMove()
    {
        _moveTween = null;

        MapMovementLogic.Input(new MapMovementLogicState.Input.MoveFinished());
    }

    private void SpawnPlayerCamera()
    {
        var playerCamera = _playerCameraScene.Instantiate<Camera3D>();
        AddChild(playerCamera);
    }

    private void SpawnPlayerMovementController()
    {
        var controller = _playerMovementController.Instantiate<PlayerMovementController>();
        AddChild(controller);
    }

    private void AnimateTurn(Vector2I turnTo)
    {
        var targetYaw = GetNearestYaw(
            Rotation.Y,
            FacingDirectionToYaw(turnTo)
        );

        _turnTween?.Kill();

        var turnDuration = MapMovementLogic.Data.MoveDuration;
        if (turnDuration <= 0)
        {
            Rotation = Rotation with { Y = targetYaw };
            FinishTurn();
            return;
        }

        _turnTween = CreateTween();
        _turnTween.TweenProperty(this, "rotation:y", targetYaw, turnDuration);
        _turnTween.Finished += FinishTurn;
    }

    private void FinishTurn()
    {
        _turnTween = null;

        MapMovementLogic.Input(new MapMovementLogicState.Input.TurnFinished());
    }

    public void OnExitTree()
    {
        _moveTween?.Kill();
        _moveTween = null;
        _turnTween?.Kill();
        _turnTween = null;

        _mapMovementBinding?.Dispose();
        _mapRepoBinding?.Dispose();
        _mapMoveDurationBinding?.Dispose();
        MapMovementLogic.Dispose();
    }

    private static float GetNearestYaw(float currentYaw, float targetYaw)
    {
        var difference = Mathf.Wrap(
            targetYaw - currentYaw,
            -Mathf.Pi,
            Mathf.Pi
        );

        return currentYaw + difference;
    }

    public void StartCooldownTimer()
    {
        CooldownTimer.WaitTime = MapMovementLogic.Data.MoveCooldown;
        CooldownTimer.OneShot = true;
        CooldownTimer.Timeout += CooldownTimerFinished;
        CooldownTimer.Start();
    }

    public void CooldownTimerFinished()
    {
        CooldownTimer.Timeout -= CooldownTimerFinished;
        MapMovementLogic.Input(new MapMovementLogicState.Input.CooldownFinished());
    }
}
