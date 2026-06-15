namespace Labyrinth;

using System;
using Godot;

[GlobalClass]
public partial class ModifyResourceBattleEffectResource :
    BattleEffectResource
{
    [Export] public BattleResource ResourceType { get; set; }
    [Export] public int Amount { get; set; }
    [Export] public string ScaleBySourceStatusId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new ModifyResourceEffectDefinition(
            ResourceType,
            Amount,
            string.IsNullOrWhiteSpace(ScaleBySourceStatusId)
                ? null
                : new StatusStackScaleDefinition(
                    new StatusId(ScaleBySourceStatusId)
                )
        );
}
