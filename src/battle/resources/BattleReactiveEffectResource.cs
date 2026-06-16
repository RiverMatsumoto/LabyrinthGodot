namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Authors a catalog reactive effect with trigger metadata, conditions, and effects.
/// </summary>
[GlobalClass]
public partial class BattleReactiveEffectResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public ReactiveEffectTrigger Trigger { get; set; }
    [Export] public ReactiveEffectSchedule Schedule { get; set; }
    [Export] public ReactiveEffectTargetPolicy TargetPolicy { get; set; }
    [Export] public int Priority { get; set; }
    [Export] public int Uses { get; set; } = -1;
    [Export]
    public Array<BattleReactiveEffectConditionResource> Conditions { get; set; } = [];
    [Export]
    public Array<BattleEffectResource> Effects { get; set; } = [];

    public ReactiveEffectDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("ReactiveEffect id is required.");
        }
        return new ReactiveEffectDefinition(
            new ReactiveEffectId(Id),
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
