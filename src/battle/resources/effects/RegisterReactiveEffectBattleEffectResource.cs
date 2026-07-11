namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class RegisterReactiveEffectBattleEffectResource :
    BattleEffectResource
{
    [Export] public string ReactiveEffectId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new RegisterReactiveEffectEffectDefinition(new ReactiveEffectId(ReactiveEffectId));
}
