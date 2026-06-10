namespace Labyrinth;

using System;
using System.Collections.Generic;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;
using Chickensoft.LogicBlocks.Auto;
using Godot;

public interface IMapMovementLogic : ILogicBlock
{
    MapMovementLogic.Data Data { get; }
    void RequestMove(Vector2I direction);
    void RequestRelativeMove(RelativeMoveDirection direction);
    void RequestTurn(TurnDirection turnDirection);
    void Enable();
    void Disable();
}

[Meta]
public partial class MapMovementLogic : AutoBlock, IMapMovementLogic
{
    public MapMovementLogic()
    {
        Set(new Data()
        {
            EntityId = new MapEntityId("default_entity"),
            MoveDuration = 0.3,
            MoveCooldown = 0.08,
        });
        Preallocate<MapMovementLogicState>();
    }

    Data IMapMovementLogic.Data => Get<Data>();

    public override void OnStart()
    {
    }

    public void RequestMove(Vector2I direction) =>
        Input(new MapMovementLogicState.Input.MoveRequested(direction));

    public void RequestRelativeMove(RelativeMoveDirection direction) =>
        Input(new MapMovementLogicState.Input.RelativeMoveRequested(direction));

    public void Enable() =>
        Input(new MapMovementLogicState.Input.Enable());

    public void Disable() =>
        Input(new MapMovementLogicState.Input.Disable());

    public void RequestTurn(TurnDirection turnDirection) =>
        Input(new MapMovementLogicState.Input.TurnRequested(turnDirection));
}
