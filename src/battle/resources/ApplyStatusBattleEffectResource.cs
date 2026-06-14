namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class ApplyStatusBattleEffectResource :
    BattleEffectResource
{
    [Export] public string StatusId { get; set; } = "";
    [Export] public int Stacks { get; set; } = 1;
    [Export] public int Duration { get; set; }
    [Export(PropertyHint.Range, "0,1,0.01")]
    public double BaseChance { get; set; } = 1;

    public override BattleEffectDefinition Compile() =>
        new ApplyStatusEffectDefinition(
            new StatusId(StatusId),
            Math.Max(1, Stacks),
            Math.Max(0, Duration),
            Math.Clamp(BaseChance, 0, 1)
        );
}
