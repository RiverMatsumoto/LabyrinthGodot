namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class BattleUnit
{
    public BattleUnit(BattleBattlerSeed seed)
    {
        Id = seed.Id;
        Name = seed.Name;
        Team = seed.Team;
        Position = seed.Position;
        Stats = seed.Stats;
        Hp = Math.Clamp(seed.Hp, 0, seed.Stats.MaxHp);
        Tp = Math.Clamp(seed.Tp, 0, seed.Stats.MaxTp);
        ActionIds = seed.ActionIds.ToArray();
        ReactiveEffectIds = seed.ReactiveEffectIds.ToArray();
        StatusResistances = new Dictionary<StatusId, double>(
            seed.StatusResistanceValues
        );
        StatusWeaknesses = new Dictionary<StatusId, double>(
            seed.StatusWeaknessValues
        );
        DamageTypeResistances = new Dictionary<DamageType, double>(
            seed.DamageResistanceValues
        );
        DamageTypeWeaknesses = new Dictionary<DamageType, double>(
            seed.DamageWeaknessValues
        );
    }

    public BattlerId Id { get; }
    public string Name { get; }
    public BattleTeam Team { get; }
    public PartyPosition Position { get; }
    public BattleStats Stats { get; }
    public int Hp { get; set; }
    public int Tp { get; set; }
    public IReadOnlyList<ActionId> ActionIds { get; }
    public IReadOnlyList<ReactiveEffectId> ReactiveEffectIds { get; }
    public Dictionary<StatusId, double> StatusResistances { get; }
    public Dictionary<StatusId, double> StatusWeaknesses { get; }
    public Dictionary<DamageType, double> DamageTypeResistances { get; }
    public Dictionary<DamageType, double> DamageTypeWeaknesses { get; }
    public Dictionary<StatusId, RuntimeStatus> Statuses { get; } = [];
    public bool IsAlive => Hp > 0;

    public bool PreventsAction(BattleCatalog catalog) =>
        Statuses.Keys.Any(id => catalog.GetStatus(id).PreventsAction);

    public BattleUnitView View() => new(
        Id,
        Name,
        Team,
        Position,
        Stats,
        Hp,
        Tp,
        ActionIds
    );
}

internal sealed class RuntimeStatus(
    StatusId id,
    int stacks,
    int remainingTurns,
    double power
)
{
    public StatusId Id { get; } = id;
    public int Stacks { get; set; } = stacks;
    public int RemainingTurns { get; set; } = remainingTurns;
    public double Power { get; set; } = power;
}

internal sealed class RuntimeReactiveEffect(
    long registrationId,
    BattlerId ownerId,
    ReactiveEffectDefinition definition,
    StatusId? sourceStatusId
)
{
    public long RegistrationId { get; } = registrationId;
    public BattlerId OwnerId { get; } = ownerId;
    public ReactiveEffectDefinition Definition { get; } = definition;
    public StatusId? SourceStatusId { get; } = sourceStatusId;
    public int RemainingUses { get; private set; } = definition.Uses;
    public bool HasUses => RemainingUses != 0;

    public void Consume()
    {
        if (RemainingUses > 0)
        {
            RemainingUses--;
        }
    }
}
