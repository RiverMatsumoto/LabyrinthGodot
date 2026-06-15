namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class RegisterReactionBattleEffectResource :
    BattleEffectResource
{
    [Export] public string ReactionId { get; set; } = "";

    public override BattleEffectDefinition Compile() =>
        new RegisterReactionEffectDefinition(new ReactionId(ReactionId));
}
