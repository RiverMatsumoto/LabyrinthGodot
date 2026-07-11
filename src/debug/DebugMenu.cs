#if DEBUG
namespace Labyrinth;

using System;
using Chickensoft.AutoInject;
using Chickensoft.GodotNodeInterfaces;
using Chickensoft.Introspection;
using Godot;

[Meta(typeof(IAutoNode))]
public partial class DebugMenu : Control, IControl
{
    public override void _Notification(int what) => this.Notify(what);
    [Node] private ICheckButton EnableOperationDebugOutput { get; set; } = default!;

    public void OnResolved()
    {
        EnableOperationDebugOutput.Toggled += SyncEnableOperationDebugOutput;
    }

    public override void _Input(InputEvent @event)
    {
        if (Input.IsActionJustPressed(GameInputs.Debug2))
        {
            Visible = !Visible;
        }
    }

    public void OnExitTree()
    {
        EnableOperationDebugOutput.Toggled -= SyncEnableOperationDebugOutput;
    }

    public void SyncEnableOperationDebugOutput(bool value)
    {
        BattleOperationExecutor.EnableOperationExecutionDebugOutput = value;
    }
}
#endif
