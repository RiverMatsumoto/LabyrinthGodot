namespace Labyrinth;

using Chickensoft.LogicBlocks;
using Godot;

public abstract partial class MenuSubmenu<TState> : LogicControl<TState>
    where TState : LogicBlockState
{
    protected abstract IMenuHubLogic MenuHubLogic { get; }

    protected abstract Button BackButton { get; set; }

    protected override ILogicBlock LogicBlock => MenuHubLogic;
    protected override Control DefaultFocusControl => BackButton;

    public override void OnResolved()
    {
        BackButton.Pressed += GoBack;
        base.OnResolved();
    }

    public override void OnExitTree()
    {
        BackButton.Pressed -= GoBack;
        base.OnExitTree();
    }

    private void GoBack() =>
        MenuHubLogic.Input(new MenuHubLogicState.Input.Back());
}
