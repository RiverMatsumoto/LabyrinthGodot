namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class DamageTypeResistanceResource : Resource
{
    [Export]
    public DamageType DamageType { get; set; }
    [Export(PropertyHint.Range, "0,1.0")]
    public double Multiplier { get; set; }
}
