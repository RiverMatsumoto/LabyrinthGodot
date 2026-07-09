namespace Labyrinth;

using Chickensoft.AutoInject;
using Chickensoft.Introspection;
using Godot;

public enum TargetSelectionCaretMode
{
    Party,
    Enemy,
}

[Meta(typeof(IAutoNode))]
public partial class TargetSelectionCaret : Control
{
    public override void _Notification(int what) => this.Notify(what);

    [Node] public Control PartyCorners { get; set; } = default!;
    [Node] public Control EnemyArrow { get; set; } = default!;

    public void SetMode(TargetSelectionCaretMode mode)
    {
        PartyCorners.Visible = mode == TargetSelectionCaretMode.Party;
        EnemyArrow.Visible = mode == TargetSelectionCaretMode.Enemy;
    }
}
