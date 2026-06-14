namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class BattleStatModifierResource : Resource
{
    [Export] public BattleStat Stat { get; set; }
    [Export] public int Amount { get; set; }

    public StatModifier Compile() => new(Stat, Amount);
}
