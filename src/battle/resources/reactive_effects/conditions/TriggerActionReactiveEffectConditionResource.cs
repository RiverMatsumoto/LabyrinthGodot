namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class TriggerActionReactiveEffectConditionResource :
    BattleReactiveEffectConditionResource
{
    [Export] public string ActionId { get; set; } = "";

    public override ReactiveEffectConditionDefinition Compile() =>
        new TriggerActionConditionDefinition(new ActionId(ActionId));
}
