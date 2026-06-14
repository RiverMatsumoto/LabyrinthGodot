namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleEncounterResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export]
    public Array<BattleEnemyResource> Enemies { get; set; } = [];
    [Export] public int Experience { get; set; }
    [Export] public int Currency { get; set; }
    [Export] public Array<string> ItemIds { get; set; } = [];

    public EncounterDefinition Compile(BattleCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Encounter id is required.");
        }
        var enemies = Enemies.Select(enemy => enemy.Compile(catalog)).ToArray();
        if (enemies.Length == 0)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' requires an enemy."
            );
        }
        return new EncounterDefinition(
            new EncounterId(Id),
            enemies,
            new BattleReward(
                Math.Max(0, Experience),
                Math.Max(0, Currency),
                ItemIds.Select(id => new EquipmentId(id)).ToArray()
            )
        );
    }
}
