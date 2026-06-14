namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.SaveFileBuilder;
using Chickensoft.Sync.Primitives;
using Godot;

public interface IMapMovement : INode3D,
    IProvide<IMapMovementLogic>,
    IProvide<ISaveChunk<MapMovementData>>
{
    MapEntityId EntityId { get; }
    IMapMovementLogic MapMovementLogic { get; }
    bool IsEnabled { get; }
    bool IsMoving { get; }

    void Initialize(MapEntityId id, MapEntityPose startPose, bool isPlayer);
}

[Meta(typeof(IAutoNode))]
public partial class MapMovement : Node3D, IMapMovement
{
    public override void _Notification(int what) => this.Notify(what);

    private bool _isInitialized;
    private Tween? _moveTween;
    private Tween? _turnTween;
    private MapEntityPose _startPose;

    public IMapMovementLogic MapMovementLogic { get; } = new MapMovementLogic();
    IMapMovementLogic IProvide<IMapMovementLogic>.Value() => MapMovementLogic;
    private MapMovementLogic.Binding? _mapMovementBinding;
    private LogicBlock.Binding? _gameBinding;
    private AutoValue<bool>.Binding? _isInMenuBinding;

    public MapEntityId EntityId => MapMovementLogic.Data.EntityId;
    public bool IsEnabled =>
        MapMovementLogic.State is not MapMovementLogicState.Disabled;
    public bool IsMoving =>
        MapMovementLogic.State is MapMovementLogicState.Moving;

    [Dependency]
    public IGridMap GridMap => this.DependOn<IGridMap>();

    #region State
    [Dependency] public IGameLogic GameLogic => this.DependOn<IGameLogic>();
    [Dependency] public IGameRepo GameRepo => this.DependOn<IGameRepo>();
    [Dependency] public IMapRepo MapRepo => this.DependOn<IMapRepo>();
    #endregion State

    #region Save
    [Dependency]
    public ISaveChunk<GameData> GameChunk =>
        this.DependOn<ISaveChunk<GameData>>();
    public ISaveChunk<MapMovementData> MapMovementChunk { get; set; } =
        default!;
    ISaveChunk<MapMovementData>
        IProvide<ISaveChunk<MapMovementData>>.Value() => MapMovementChunk;
    #endregion Save

    private Camera3D _playerCamera { get; set; } = default!;
    private PlayerMovementController _playerMovementController { get; set; } = default!;

    private PackedScene _playerCameraScene =
        GD.Load<PackedScene>("res://src/map_movement/PlayerCamera.tscn");
    private PackedScene _playerMovementControllerScene =
        GD.Load<PackedScene>("res://src/map_movement/PlayerMovementController.tscn");

    [Node]
    public Timer CooldownTimer { get; set; } = default!;

    public void Setup()
    {
        MapMovementChunk = new SaveChunk<MapMovementData>(
            onSave: (_) =>
            {
                var data = MapMovementLogic.Data;
                return new MapMovementData
                {
                    MoveDuration = data.MoveDuration,
                    MoveCooldown = data.MoveCooldown,
                };
            },
            onLoad: (_, data) =>
                MapMovementLogic.ApplyLoadedSettings(new MapMovementSettings(
                    MoveDuration: data.MoveDuration,
                    MoveCooldown: data.MoveCooldown
                ))
        );
    }

    public void OnResolved()
    {
        MapMovementLogic.Set(MapRepo);
        MapMovementLogic.Set(GameRepo);

#if DEBUG
        AddToGroup(DebugConsole.MapMovementGroup);
#endif

        _mapMovementBinding = MapMovementLogic.Bind()
            .OnOutput((in MapMovementLogicState.Output.MoveStarted output) =>
                AnimateMove(output.Move, output.Duration))
            .OnOutput((in MapMovementLogicState.Output.TurnStarted output) =>
                AnimateTurn(output.FacingDirection, output.Duration))
            .OnOutput((in MapMovementLogicState.Output.CooldownStarted output) =>
                StartCooldownTimer(output.Duration));

        _gameBinding = GameLogic.Bind()
            .OnEnter<GameLogicState.Labyrinth>(_ =>
                MapMovementLogic.Enable())
            .OnExit<GameLogicState.Labyrinth>(_ =>
                MapMovementLogic.Disable());

        if (MapMovementLogic.Data.IsPlayer)
        {
            GameChunk.AddChunk(MapMovementChunk);
        }

        ApplyStartPosition();

        this.Provide();

        MapMovementLogic.Start<MapMovementLogicState.Disabled>();

        _isInMenuBinding = GameRepo.IsInMenu.Bind()
            .OnValue((isInMenu) =>
            {
                if (isInMenu)
                    MapMovementLogic.Input(new MapMovementLogicState.Input.Disable());
                else
                    MapMovementLogic.Input(new MapMovementLogicState.Input.Enable());
            });
    }

    public void Initialize(
        MapEntityId id,
        MapEntityPose startPose,
        bool isPlayer
    )
    {
        if (_isInitialized || id.IsEmpty)
        {
            GD.PrintErr("MapMovement: initialization error");
            return;
        }

        MapMovementLogic.Initialize(id, isPlayer);
        _startPose = startPose;
        _isInitialized = true;
        Name = id.Value;

        if (isPlayer)
        {
            SpawnPlayerNodes();
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

    private void AnimateMove(GridMove move, double duration)
    {
        var target = GridToGlobalPosition(move.To);

        _moveTween?.Kill();

        if (duration <= 0)
        {
            GlobalPosition = target;
            FinishMove();
            return;
        }

        _moveTween = CreateTween();
        _moveTween.TweenProperty(
            this,
            "global_position",
            target + new Vector3(0, PlayerPositionGridOffset(), 0),
            duration
        );
        _moveTween.Finished += FinishMove;
    }

    private static float PlayerPositionGridOffset() => 0.5f;

    private Vector3 GridToGlobalPosition(Vector2I gridPosition) =>
        GridMap.ToGlobal(GridMap.MapToLocal(GridToMapCell(gridPosition)));

    private void ApplyStartPosition()
    {
        if (!_isInitialized)
        {
            return;
        }

        GlobalPosition = GridToGlobalPosition(_startPose.Position)
            + new Vector3(0, PlayerPositionGridOffset(), 0);

        Rotation = Rotation with
        {
            Y = FacingDirectionToYaw(_startPose.FacingDirection),
        };
    }

    private void FinishMove()
    {
        _moveTween = null;

        MapMovementLogic.Input(new MapMovementLogicState.Input.MoveFinished());
    }

    private void SpawnPlayerNodes()
    {
        _playerCamera = _playerCameraScene.Instantiate<Camera3D>();
        _playerMovementController = _playerMovementControllerScene.Instantiate<PlayerMovementController>();
        _playerMovementController.PlayerCamera = _playerCamera;
        AddChild(_playerCamera);
        AddChild(_playerMovementController);
    }

    private void AnimateTurn(Vector2I turnTo, double duration)
    {
        var targetYaw = GetNearestYaw(
            Rotation.Y,
            FacingDirectionToYaw(turnTo)
        );

        _turnTween?.Kill();

        if (duration <= 0)
        {
            Rotation = Rotation with { Y = targetYaw };
            FinishTurn();
            return;
        }

        _turnTween = CreateTween();
        _turnTween.TweenProperty(this, "rotation:y", targetYaw, duration);
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
        CooldownTimer.Stop();

        _mapMovementBinding?.Dispose();
        _gameBinding?.Dispose();
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

    private void StartCooldownTimer(double duration)
    {
        CooldownTimer.WaitTime = duration;
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
