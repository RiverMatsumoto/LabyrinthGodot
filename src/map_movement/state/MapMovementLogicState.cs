namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record MapMovementLogicState
    : LogicBlockState,
        IGet<MapMovementLogicState.Input.SettingsLoaded>
{
    protected MapMovementLogic.Data Data => Get<MapMovementLogic.Data>();

    public Type On(in Input.SettingsLoaded input)
    {
        Get<IGameRepo>().SetMapMovementSettings(input.Settings);
        return ToSelf();
    }
}
