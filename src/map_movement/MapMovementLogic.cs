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
        Preallocate<MapMovementLogicState>();
        Set(new Data());
    }

    Data IMapMovementLogic.Data => Get<Data>();

    public override IEnumerable<IDisposable> OnStartSubscriptions()
    {
        yield return Get<IGameRepo>().CurrentGameState.Bind()
            .OnValue(gameState =>
            {
                switch (gameState)
                {
                    case GameMode.DungeonExploration:
                        Input(new MapMovementLogicState.Input.Enable());
                        break;
                    default:
                        Input(new MapMovementLogicState.Input.Disable());
                        break;
                }

            });
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
