namespace Labyrinth;

using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class RegisterReactionBattleEffectResource :
    BattleEffectResource
{
    [Export] public string ReactionId { get; set; } = "";
    [Export] public ReactionWindow Window { get; set; }
    [Export]
    public ReactionInsertionPolicy InsertionPolicy { get; set; }
    [Export] public ReactionTargetPolicy TargetPolicy { get; set; }
    [Export] public int Priority { get; set; }
    [Export] public int Uses { get; set; } = -1;
    [Export]
    public Array<BattleEffectResource> Effects { get; set; } = [];

    public override BattleEffectDefinition Compile() =>
        new RegisterReactionEffectDefinition(new ReactionDefinition(
            ReactionId,
            Window,
            InsertionPolicy,
            TargetPolicy,
            Priority,
            Effects.Select(effect => effect.Compile()).ToArray(),
            Uses
        ));
}
