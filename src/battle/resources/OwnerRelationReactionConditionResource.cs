namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class OwnerRelationReactionConditionResource :
    BattleReactionConditionResource
{
    [Export] public ReactionOwnerRelation Relation { get; set; }

    public override ReactionConditionDefinition Compile() =>
        new OwnerRelationConditionDefinition(Relation);
}
