namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class ModifyResourceBattleEffectResource :
    BattleEffectResource
{
    [Export] public BattleResource ResourceType { get; set; }
    [Export] public int Amount { get; set; }

    public override BattleEffectDefinition Compile() =>
        new ModifyResourceEffectDefinition(ResourceType, Amount);
}
