namespace Labyrinth;

using Godot;

[GlobalClass]
public partial class StatusWeaknessResource : Resource
{
    [Export] public string StatusId { get; set; } = "";
    [Export(PropertyHint.Range, "0,1,0.01")]
    public double Multiplier { get; set; }
}
