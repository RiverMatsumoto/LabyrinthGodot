namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Authors a catalog reaction with trigger metadata, conditions, and effects.
/// </summary>
[GlobalClass]
public partial class BattleReactionResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public ReactionTrigger Trigger { get; set; }
    [Export] public ReactionSchedule Schedule { get; set; }
    [Export] public ReactionTargetPolicy TargetPolicy { get; set; }
    [Export] public int Priority { get; set; }
    [Export] public int Uses { get; set; } = -1;
    [Export]
    public Array<BattleReactionConditionResource> Conditions { get; set; } = [];
    [Export]
    public Array<BattleEffectResource> Effects { get; set; } = [];

    public ReactionDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Reaction id is required.");
        }
        return new ReactionDefinition(
            new ReactionId(Id),
            Trigger,
            Schedule,
            TargetPolicy,
            Priority,
            Conditions.Select(condition => condition.Compile()).ToArray(),
            Effects.Select(effect => effect.Compile()).ToArray(),
            Uses
        );
    }
}
