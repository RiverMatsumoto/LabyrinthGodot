namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class OwnerRelationReactiveEffectConditionResource :
    BattleReactiveEffectConditionResource
{
    [Export] public ReactiveEffectOwnerRelation Relation { get; set; }

    public override ReactiveEffectConditionDefinition Compile() =>
        new OwnerRelationConditionDefinition(Relation);
}
