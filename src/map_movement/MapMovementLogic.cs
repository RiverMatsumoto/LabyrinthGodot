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
    void Initialize(MapEntityId id, bool isPlayer);
    void ApplyLoadedSettings(MapMovementSettings settings);
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
        yield return Get<IGameRepo>().MapMovementSettings.Bind()
            .OnValue(settings =>
            {
                var data = Get<Data>();
                data.MoveDuration = settings.MoveDuration;
                data.MoveCooldown = settings.MoveCooldown;
            });
    }

    public void Initialize(MapEntityId id, bool isPlayer)
    {
        var data = Get<Data>();
        data.EntityId = id;
        data.IsPlayer = isPlayer;
    }

    public void ApplyLoadedSettings(MapMovementSettings settings) =>
        Input(new MapMovementLogicState.Input.SettingsLoaded(settings));

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
