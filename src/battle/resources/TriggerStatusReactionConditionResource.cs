namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class TriggerStatusReactionConditionResource :
    BattleReactionConditionResource
{
    [Export] public string StatusId { get; set; } = "";

    public override ReactionConditionDefinition Compile() =>
        new TriggerStatusConditionDefinition(new StatusId(StatusId));
}
