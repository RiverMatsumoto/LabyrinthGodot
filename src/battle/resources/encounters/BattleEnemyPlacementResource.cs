namespace Labyrinth;

using System;
using Godot;

/// <summary>
/// Places reusable enemy data into one encounter with a unique battler ID.
/// </summary>
[GlobalClass]
public partial class BattleEnemyPlacementResource : Resource
{
    [Export] public string BattlerId { get; set; } = "";
    [Export] public PartyRow Row { get; set; }
    [Export(PropertyHint.Range, "0,2,1")]
    public int Slot { get; set; }
    [Export] public BattleEnemyResource? Enemy { get; set; }

    public BattleEnemyPlacement Compile(BattleCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(BattlerId))
        {
            throw new InvalidOperationException(
                "Enemy placement battler id is required."
            );
        }
        if (Slot is < 0 or > 2)
        {
            throw new InvalidOperationException(
                $"Enemy placement '{BattlerId}' has invalid slot '{Slot}'."
            );
        }
        if (Enemy is null)
        {
            throw new InvalidOperationException(
                $"Enemy placement '{BattlerId}' requires enemy data."
            );
        }
        return new BattleEnemyPlacement(
            new BattlerId(BattlerId),
            new PartyPosition(Row, Slot),
            Enemy.Compile(catalog)
        );
    }
}
