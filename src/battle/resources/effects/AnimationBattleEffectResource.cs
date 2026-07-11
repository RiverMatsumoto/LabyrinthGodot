namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class AnimationBattleEffectResource : BattleEffectResource
{
    [Export] public string AnimationId { get; set; } = "";
    [Export] public bool Wait { get; set; } = true;

    public override BattleEffectDefinition Compile() =>
        new PlayAnimationEffectDefinition(AnimationId, Wait);
}
