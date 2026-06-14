namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleEnemyResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public PartyRow Row { get; set; }
    [Export] public int Slot { get; set; }
    [Export] public BattleStatsResource Stats { get; set; } = new();
    [Export] public int Hp { get; set; } = 100;
    [Export] public int Tp { get; set; } = 20;
    [Export] public Array<string> ActionIds { get; set; } = [];
    [Export]
    public Array<StatusResistanceResource> Resistances { get; set; } = [];

    public BattleBattlerSeed Compile(BattleCatalog catalog)
    {
        var actions = ActionIds.Select(id => new ActionId(id)).ToArray();
        foreach (var actionId in actions)
        {
            _ = catalog.GetAction(actionId);
        }
        return new BattleBattlerSeed(
            new BattlerId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            BattleTeam.Enemy,
            new PartyPosition(Row, Slot),
            Stats.Compile(),
            Hp,
            Tp,
            actions,
            Resistances.ToDictionary(
                resistance => new StatusId(resistance.StatusId),
                resistance => Math.Clamp(resistance.Value, 0, 1)
            )
        );
    }
}
