namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleActionResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public BattleTargetRule TargetRule { get; set; }
    [Export] public int TpCost { get; set; }
    [Export] public int Priority { get; set; }
    [Export] public BattleRange Range { get; set; }
    [Export] public RetargetPolicy RetargetPolicy { get; set; }
    [Export]
    public Array<BattleEffectResource> Effects { get; set; } = [];

    public BattleActionDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Action id is required.");
        }
        return new BattleActionDefinition(
            new ActionId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            TargetRule,
            Effects.Select(effect => effect.Compile()).ToArray(),
            Math.Max(0, TpCost),
            Priority,
            Range,
            RetargetPolicy
        );
    }
}
