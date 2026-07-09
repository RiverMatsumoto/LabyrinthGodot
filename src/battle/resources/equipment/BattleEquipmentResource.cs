namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleEquipmentResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export]
    public Array<BattleStatModifierResource> Modifiers { get; set; } = [];

    public EquipmentDefinition Compile()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Equipment id is required.");
        }
        return new EquipmentDefinition(
            new EquipmentId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            Modifiers.Select(modifier => modifier.Compile()).ToArray()
        );
    }
}
