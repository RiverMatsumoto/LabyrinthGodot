namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class OwnerHasStatusReactiveEffectConditionResource :
    BattleReactiveEffectConditionResource
{
    [Export] public string StatusId { get; set; } = "";
    [Export] public int MinimumStacks { get; set; } = 1;

    public override ReactiveEffectConditionDefinition Compile() =>
        new OwnerHasStatusConditionDefinition(
            new StatusId(StatusId),
            Math.Max(1, MinimumStacks)
        );
}
