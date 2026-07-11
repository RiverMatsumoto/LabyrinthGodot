namespace Labyrinth;

using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class MatchesDamageTypeCondition :
    BattleReactiveEffectConditionResource
{
    [Export] public required Array<DamageType> DamageType { get; set; } = [];

    public override ReactiveEffectConditionDefinition Compile() =>
        new MatchesDamageTypeConditionDefinition(DamageType.ToArray());
}
