namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IPlayerMovementController : INode;


[Meta(typeof(IAutoNode))]
public partial class PlayerMovementController : Node, IPlayerMovementController
{
    private const float CameraAimRange = 2.0f;
    private const float CameraAimSmoothing = 12.0f;
    private const float CameraAimDeadzone = 0.05f;

    private Camera3D? _playerCamera;
    private Transform3D _cameraBaseTransform;
    private bool _hasCameraBaseTransform;

    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    public IMapMovementLogic MapMovementLogic => this.DependOn<IMapMovementLogic>();

    public Camera3D PlayerCamera
    {
        get => _playerCamera!;
        set
        {
            _playerCamera = value;
            _hasCameraBaseTransform = false;
        }
    }

    public override void _Process(double delta)
    {
        UpdateCameraAim(delta);

        var moveDirection = GetRequestedMoveDirection();

        if (moveDirection is not null)
        {
            MapMovementLogic.RequestRelativeMove(moveDirection.Value);
            GetViewport().SetInputAsHandled();
            return;
        }

        TurnDirection? turnDirection = GetRequestedTurnDirection();

        if (turnDirection is not null)
        {
            MapMovementLogic.RequestTurn(turnDirection.Value);
            GetViewport().SetInputAsHandled();
        }
    }

    public static Vector3 CameraAimInputToLocalOffset(Vector2 input)
    {
        var clampedInput = input.LimitLength();
        return new Vector3(
            clampedInput.X * CameraAimRange,
            0.0f,
            clampedInput.Y * CameraAimRange
        );
    }

    private void UpdateCameraAim(double delta)
    {
        if (_playerCamera is null)
        {
            return;
        }

        if (!_hasCameraBaseTransform)
        {
            _cameraBaseTransform = _playerCamera.Transform;
            _hasCameraBaseTransform = true;
        }

        var input = Input.GetVector(
            GameInputs.PanCameraLeft,
            GameInputs.PanCameraRight,
            GameInputs.PanCameraUp,
            GameInputs.PanCameraDown
        );
        var weight =
            1.0f - Mathf.Exp(-CameraAimSmoothing * (float)delta);

        if (input.Length() <= CameraAimDeadzone)
        {
            _playerCamera.Transform =
                _playerCamera.Transform.InterpolateWith(
                    _cameraBaseTransform,
                    weight
                );
            return;
        }

        if (GetParent() is not Node3D parent)
        {
            return;
        }

        var currentTransform = _playerCamera.GlobalTransform;
        var target = parent.GlobalPosition
            + (parent.GlobalBasis * CameraAimInputToLocalOffset(input));

        _playerCamera.LookAt(target, Vector3.Up);
        var targetTransform = _playerCamera.GlobalTransform;

        _playerCamera.GlobalTransform =
            currentTransform.InterpolateWith(targetTransform, weight);
    }

    private static RelativeMoveDirection? GetRequestedMoveDirection()
    {
        if (Input.IsActionPressed(GameInputs.MoveForward))
        {
            return RelativeMoveDirection.Forward;
        }

        if (Input.IsActionPressed(GameInputs.MoveBackward))
        {
            return RelativeMoveDirection.Backward;
        }

        if (Input.IsActionPressed(GameInputs.MoveLeft))
        {
            return RelativeMoveDirection.Left;
        }

        if (Input.IsActionPressed(GameInputs.MoveRight))
        {
            return RelativeMoveDirection.Right;
        }

        return null;
    }

    private static TurnDirection? GetRequestedTurnDirection()
    {
        if (Input.IsActionPressed(GameInputs.TurnLeft))
        {
            return TurnDirection.Left;
        }

        if (Input.IsActionPressed(GameInputs.TurnRight))
        {
            return TurnDirection.Right;
        }

        return null;
    }
}
