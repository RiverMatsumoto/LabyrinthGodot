namespace Labyrinth;

using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Authors reusable enemy stats, actions, passives, and affinities.
/// </summary>
[GlobalClass]
public partial class BattleEnemyResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public BattleStatsResource Stats { get; set; } = new();
    [Export] public int Hp { get; set; } = 100;
    [Export] public int Tp { get; set; } = 20;
    [Export] public Array<BattleActionResource> Actions { get; set; } = [];
    [Export] public Array<string> ReactionIds { get; set; } = [];
    [Export]
    public Array<StatusResistanceResource> StatusResistances { get; set; } = [];
    [Export]
    public Array<StatusWeaknessResource> StatusWeaknesses { get; set; } = [];
    [Export]
    public Array<DamageTypeResistanceResource> DamageTypeResistances
    {
        get;
        set;
    } = [];
    [Export]
    public Array<DamageTypeWeaknessResource> DamageTypeWeaknesses
    {
        get;
        set;
    } = [];

    public BattleEnemyDefinition Compile(BattleCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Enemy id is required.");
        }
        var actions = Actions.Select(action =>
        {
            if (action is null)
            {
                throw new InvalidOperationException(
                    $"Enemy '{Id}' has a missing action."
                );
            }
            return new ActionId(action.Id);
        }).ToArray();
        if (actions.Distinct().Count() != actions.Length)
        {
            throw new InvalidOperationException(
                $"Enemy '{Id}' has duplicate action references."
            );
        }
        foreach (var actionId in actions)
        {
            _ = catalog.GetAction(actionId);
        }
        var reactions = ReactionIds
            .Select(id => new ReactionId(id))
            .ToArray();
        foreach (var reactionId in reactions)
        {
            _ = catalog.GetReaction(reactionId);
        }
        foreach (var affinity in StatusResistances)
        {
            _ = catalog.GetStatus(new StatusId(affinity.StatusId));
        }
        foreach (var affinity in StatusWeaknesses)
        {
            _ = catalog.GetStatus(new StatusId(affinity.StatusId));
        }
        return new BattleEnemyDefinition(
            new EnemyId(Id),
            string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName,
            Stats.Compile(),
            Hp,
            Tp,
            actions,
            reactions,
            StatusResistances.ToDictionary(
                resistance => new StatusId(resistance.StatusId),
                resistance => Math.Clamp(resistance.Multiplier, 0, 1)
            ),
            StatusWeaknesses.ToDictionary(
                weakness => new StatusId(weakness.StatusId),
                weakness => Math.Clamp(weakness.Multiplier, 0, 1)
            ),
            DamageTypeResistances.ToDictionary(
                resistance => resistance.DamageType,
                resistance => Math.Clamp(resistance.Multiplier, 0, 1)
            ),
            DamageTypeWeaknesses.ToDictionary(
                weakness => weakness.DamageType,
                weakness => Math.Clamp(weakness.Multiplier, 0, 1)
            )
        );
    }
}
