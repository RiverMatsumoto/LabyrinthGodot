namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.LogicBlocks;
using Chickensoft.Sync.Disposables;
using Godot;

public abstract partial class LogicControl<TState> : Control where TState : LogicBlockState
{
    public override void _Notification(int what) => this.Notify(what);

    protected CompositeDisposable CompositeDisposable { get; } = new();
    protected abstract ILogicBlock LogicBlock { get; }
    protected virtual Control? DefaultFocusControl => null;

    private Control? _lastFocusedControl;
    private TState? _activeState;

    public virtual void OnResolved()
    {
        Deactivate();

        LogicBlock.Bind()
            .OnEnter<TState>(EnterState)
            .OnExit<TState>(ExitState)
            .OnStop(() => Deactivate())
            .DisposeWith(CompositeDisposable);

        if (LogicBlock.State is TState state)
        {
            Activate(state);
        }
    }

    public virtual void OnExitTree()
    {
        CompositeDisposable.Dispose();
    }

    protected virtual void OnActivated(TState state) { }

    protected virtual void OnDeactivated(TState state) { }

    private void EnterState(LogicBlockState state)
    {
        if (state is TState typedState)
        {
            Activate(typedState);
        }
    }

    private void ExitState(LogicBlockState state)
    {
        if (state is TState typedState)
        {
            Deactivate(typedState);
        }
    }

    private void Activate(TState state)
    {
        _activeState = state;
        ProcessMode = ProcessModeEnum.Inherit;
        Visible = true;

        OnActivated(state);
        RestoreFocus();
    }

    private void Deactivate(TState? state = null)
    {
        RememberFocus();

        var exitedState = state ?? _activeState;
        if (exitedState is not null)
        {
            OnDeactivated(exitedState);
        }

        _activeState = null;
        Visible = false;
        ProcessMode = ProcessModeEnum.Disabled;
    }

    private void RememberFocus()
    {
        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner is null || !IsAncestorOf(focusOwner))
        {
            return;
        }

        _lastFocusedControl = focusOwner;
        focusOwner.ReleaseFocus();
    }

    private void RestoreFocus()
    {
        var focusTarget = IsValidFocusTarget(_lastFocusedControl)
            ? _lastFocusedControl
            : DefaultFocusControl;

        if (IsValidFocusTarget(focusTarget))
        {
            focusTarget!.GrabFocus();
        }
    }

    private bool IsValidFocusTarget(Control? control) =>
        control is not null
        && GodotObject.IsInstanceValid(control)
        && IsAncestorOf(control)
        && control.IsVisibleInTree()
        && control.FocusMode is not FocusModeEnum.None;
}
