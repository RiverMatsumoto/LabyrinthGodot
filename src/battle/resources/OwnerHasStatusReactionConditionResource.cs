namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class OwnerHasStatusReactionConditionResource :
    BattleReactionConditionResource
{
    [Export] public string StatusId { get; set; } = "";
    [Export] public int MinimumStacks { get; set; } = 1;

    public override ReactionConditionDefinition Compile() =>
        new OwnerHasStatusConditionDefinition(
            new StatusId(StatusId),
            Math.Max(1, MinimumStacks)
        );
}
