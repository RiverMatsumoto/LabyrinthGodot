namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class RemoveStatusBattleEffectResource :
    BattleEffectResource
{
    [Export] public string StatusId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new RemoveStatusEffectDefinition(new StatusId(StatusId));
}
