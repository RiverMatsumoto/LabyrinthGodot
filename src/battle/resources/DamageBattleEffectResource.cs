namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class DamageBattleEffectResource : BattleEffectResource
{
    [Export] public DamageType DamageType { get; set; }
    [Export] public DamageMode Mode { get; set; }
    [Export] public double Power { get; set; } = 1;
    [Export] public bool CanCrit { get; set; }
    [Export] public double CritMultiplier { get; set; } = 1.8;
    [Export] public string AnimationId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new DamageEffectDefinition(
            new DamageSpec(
                DamageType,
                Mode,
                Power,
                CanCrit,
                Math.Max(1, CritMultiplier)
            ),
            AnimationId
        );
}
