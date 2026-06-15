namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class TriggerActionReactionConditionResource :
    BattleReactionConditionResource
{
    [Export] public string ActionId { get; set; } = "";

    public override ReactionConditionDefinition Compile() =>
        new TriggerActionConditionDefinition(new ActionId(ActionId));
}
