namespace Labyrinth;

using System;
using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record GameLogicState
    : LogicBlockState,
        IGet<GameLogicState.Input.SetOverlay>,
        IGet<GameLogicState.Input.SetMovementSettings>
{
    public Type On(in Input.SetOverlay input)
    {
        Get<IGameRepo>().SetMenuOverlay(input.Overlay);
        Output(new Output.OverlayChanged(input.Overlay));
        return ToSelf();
    }

    public Type On(in Input.SetMovementSettings input)
    {
        Get<IGameRepo>().SetMapMovementSettings(input.Settings);
        Output(new Output.MovementSettingsChanged(input.Settings));
        return ToSelf();
    }
}
