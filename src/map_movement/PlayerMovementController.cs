namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

public interface IPlayerMovementController : INode;


[Meta(typeof(IAutoNode))]
public partial class PlayerMovementController : Node, IPlayerMovementController
{
    public override void _Notification(int what) => this.Notify(what);

    [Dependency]
    public IMapMovementLogic MapMovementLogic =>
        this.DependOn<IMapMovementLogic>();


    public override void _Process(double delta)
    {
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
