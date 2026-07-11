namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class TriggerStatusReactiveEffectConditionResource :
    BattleReactiveEffectConditionResource
{
    [Export] public string StatusId { get; set; } = "";

    public override ReactiveEffectConditionDefinition Compile() =>
        new TriggerStatusConditionDefinition(new StatusId(StatusId));
}
