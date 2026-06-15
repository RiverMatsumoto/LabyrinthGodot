namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class HealBattleEffectResource : BattleEffectResource
{
    [Export] public int Amount { get; set; } = 1;
    [Export] public string AnimationId { get; set; } = "";
    [Export] public string ScaleBySourceStatusId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new HealEffectDefinition(
            Math.Max(0, Amount),
            AnimationId,
            string.IsNullOrWhiteSpace(ScaleBySourceStatusId)
                ? null
                : new StatusStackScaleDefinition(
                    new StatusId(ScaleBySourceStatusId)
                )
        );
}
