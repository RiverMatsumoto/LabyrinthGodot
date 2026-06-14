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

    public void OnResolved()
    {
        LogicBlock.Bind()
            .OnEnter<TState>(OnEnteredState)
            .OnExit<TState>(OnExitedState)
            .DisposeWith(CompositeDisposable);
    }

    public void OnExitTree()
    {
        CompositeDisposable.Dispose();
    }

    public virtual void OnEnteredState(LogicBlockState state)
    {
        Visible = true;
    }

    public virtual void OnExitedState(LogicBlockState state)
    {
        Visible = false;
    }
}
