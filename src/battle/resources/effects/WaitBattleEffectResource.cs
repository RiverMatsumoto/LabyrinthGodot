namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class WaitBattleEffectResource : BattleEffectResource
{
    [Export] public double Seconds { get; set; } = 0.25;

    public override BattleEffectDefinition Compile() =>
        new WaitEffectDefinition(Math.Max(0, Seconds));
}
