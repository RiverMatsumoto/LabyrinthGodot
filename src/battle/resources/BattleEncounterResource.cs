namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleEncounterResource : Resource
{
    public const int MaxEnemyPlacements = 6;

    [Export] public string Id { get; set; } = "";
    [Export]
    public Array<BattleEnemyPlacementResource> Enemies { get; set; } = [];
    [Export] public int Experience { get; set; }
    [Export] public int Currency { get; set; }
    [Export] public Array<string> ItemIds { get; set; } = [];

    public EncounterDefinition Compile(BattleCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Encounter id is required.");
        }
        if (Enemies.Count == 0)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' requires an enemy."
            );
        }
        if (Enemies.Count > MaxEnemyPlacements)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' cannot exceed {MaxEnemyPlacements} enemies."
            );
        }

        var placements = Enemies
            .Select(enemy => enemy.Compile(catalog))
            .ToArray();
        if (placements.Select(enemy => enemy.BattlerId).Distinct().Count()
            != placements.Length)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' has duplicate battler ids."
            );
        }
        if (placements.Select(enemy => enemy.Position).Distinct().Count()
            != placements.Length)
        {
            throw new InvalidOperationException(
                $"Encounter '{Id}' has duplicate enemy positions."
            );
        }

        return new EncounterDefinition(
            new EncounterId(Id),
            placements.Select(BattleBattlerSeed.FromEnemy).ToArray(),
            new BattleReward(
                Math.Max(0, Experience),
                Math.Max(0, Currency),
                ItemIds.Select(id => new EquipmentId(id)).ToArray()
            )
        );
    }
}
