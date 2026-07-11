namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class DamageStatScaleResource : Resource
{
    [Export] public BattleStat Stat { get; set; }
    [Export] public double Weight { get; set; } = 1;

    public BattleStatScaleDefinition Compile() => new(Stat, Weight);
}
