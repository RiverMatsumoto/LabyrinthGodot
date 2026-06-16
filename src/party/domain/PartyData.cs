namespace Labyrinth;

using System;
using System.Linq;
using Chickensoft.Introspection;
using Chickensoft.Serialization;

[Meta, Id("party_data")]
public partial record PartyData
{
    public const int CurrentVersion = 2;

    [Save("version")]
    public int Version { get; init; } = CurrentVersion;

    [Save("members")]
    public PartyMemberData[] Members { get; init; } = [];

    public static PartyData Empty => new();

    public static PartyData From(IPartyRepo repo) => new()
    {
        Members = repo.Members.Select(PartyMemberData.From).ToArray(),
    };
}

[Meta, Id("party_member_data")]
public partial record PartyMemberData
{
    [Save("id")]
    public string Id { get; init; } = "";
    [Save("name")]
    public string Name { get; init; } = "";
    [Save("level")]
    public int Level { get; init; } = 1;
    [Save("experience")]
    public int Experience { get; init; }
    [Save("hp")]
    public int Hp { get; init; }
    [Save("tp")]
    public int Tp { get; init; }
    [Save("row")]
    public PartyRow Row { get; init; }
    [Save("slot")]
    public int Slot { get; init; }
    [Save("stats")]
    public BattleStatsData Stats { get; init; } = new();
    [Save("actions")]
    public string[] Actions { get; init; } = [];
    [Save("equipment")]
    public string[] Equipment { get; init; } = [];
    [Save("modifiers")]
    public StatModifierData[] Modifiers { get; init; } = [];
    [Save("resistances")]
    public StatusResistanceData[] Resistances { get; init; } = [];
    [Save("status_weaknesses")]
    public StatusResistanceData[] StatusWeaknesses { get; init; } = [];
    [Save("damage_resistances")]
    public DamageAffinityData[] DamageResistances { get; init; } = [];
    [Save("damage_weaknesses")]
    public DamageAffinityData[] DamageWeaknesses { get; init; } = [];
    [Save("passive_reactive_effects")]
    public string[] PassiveReactiveEffects { get; init; } = [];
    [Save("passive_reactions")]
    public string[] LegacyPassiveReactiveEffects { get; init; } = [];

    public PartyMember ToDomain()
    {
        var member = new PartyMember
        {
            Id = new BattlerId(Id),
            Name = Name,
            Level = Math.Max(1, Level),
            Experience = Math.Max(0, Experience),
            Hp = Hp,
            Tp = Tp,
            BaseStats = Stats.ToDomain(),
        };
        member.LearnedActions.AddRange(Actions.Select(id => new ActionId(id)));
        member.Equipment.AddRange(
            Equipment.Select(id => new EquipmentId(id))
        );
        member.EquipmentModifiers.AddRange(
            Modifiers.Select(modifier => modifier.ToDomain())
        );
        foreach (var resistance in Resistances)
        {
            member.StatusResistances[new StatusId(resistance.StatusId)] =
                resistance.Value;
        }
        foreach (var weakness in StatusWeaknesses)
        {
            member.StatusWeakness[new StatusId(weakness.StatusId)] =
                weakness.Value;
        }
        foreach (var resistance in DamageResistances)
        {
            member.DamageTypeResistances[resistance.DamageType] =
                resistance.Value;
        }
        foreach (var weakness in DamageWeaknesses)
        {
            member.DamageTypeWeaknesses[weakness.DamageType] =
                weakness.Value;
        }
        var passiveReactiveEffects = PassiveReactiveEffects.Length > 0
            ? PassiveReactiveEffects
            : LegacyPassiveReactiveEffects;
        member.PassiveReactiveEffectIds.AddRange(
            passiveReactiveEffects.Select(id => new ReactiveEffectId(id))
        );
        return member;
    }

    public static PartyMemberData From(PartyMemberEntry entry) => new()
    {
        Id = entry.Member.Id.Value,
        Name = entry.Member.Name,
        Level = entry.Member.Level,
        Experience = entry.Member.Experience,
        Hp = entry.Member.Hp,
        Tp = entry.Member.Tp,
        Row = entry.Position.Row,
        Slot = entry.Position.Index,
        Stats = BattleStatsData.From(entry.Member.BaseStats),
        Actions = entry.Member.LearnedActions.Select(id => id.Value).ToArray(),
        Equipment = entry.Member.Equipment.Select(id => id.Value).ToArray(),
        Modifiers = entry.Member.EquipmentModifiers
            .Select(StatModifierData.From)
            .ToArray(),
        Resistances = entry.Member.StatusResistances
            .Select(pair => new StatusResistanceData
            {
                StatusId = pair.Key.Value,
                Value = pair.Value,
            })
            .ToArray(),
        StatusWeaknesses = entry.Member.StatusWeakness
            .Select(pair => new StatusResistanceData
            {
                StatusId = pair.Key.Value,
                Value = pair.Value,
            })
            .ToArray(),
        DamageResistances = entry.Member.DamageTypeResistances
            .Select(pair => new DamageAffinityData
            {
                DamageType = pair.Key,
                Value = pair.Value,
            })
            .ToArray(),
        DamageWeaknesses = entry.Member.DamageTypeWeaknesses
            .Select(pair => new DamageAffinityData
            {
                DamageType = pair.Key,
                Value = pair.Value,
            })
            .ToArray(),
        PassiveReactiveEffects = entry.Member.PassiveReactiveEffectIds
            .Select(id => id.Value)
            .ToArray(),
    };
}

[Meta, Id("battle_stats_data")]
public partial record BattleStatsData
{
    [Save("max_hp")]
    public int MaxHp { get; init; } = 100;
    [Save("max_tp")]
    public int MaxTp { get; init; } = 20;
    [Save("strength")]
    public int Strength { get; init; } = 10;
    [Save("technique")]
    public int Technique { get; init; } = 10;
    [Save("agility")]
    public int Agility { get; init; } = 10;
    [Save("vitality")]
    public int Vitality { get; init; } = 10;
    [Save("wisdom")]
    public int Wisdom { get; init; } = 10;
    [Save("luck")]
    public int Luck { get; init; } = 10;
    [Save("attack")]
    public int Attack { get; init; }
    [Save("defense")]
    public int Defense { get; init; }

    public BattleStats ToDomain() => new(
        MaxHp,
        MaxTp,
        Strength,
        Technique,
        Agility,
        Vitality,
        Wisdom,
        Luck,
        Attack,
        Defense
    );

    public static BattleStatsData From(BattleStats stats) => new()
    {
        MaxHp = stats.MaxHp,
        MaxTp = stats.MaxTp,
        Strength = stats.Strength,
        Technique = stats.Technique,
        Agility = stats.Agility,
        Vitality = stats.Vitality,
        Wisdom = stats.Wisdom,
        Luck = stats.Luck,
        Attack = stats.Attack,
        Defense = stats.Defense,
    };
}

[Meta, Id("stat_modifier_data")]
public partial record StatModifierData
{
    [Save("stat")]
    public BattleStat Stat { get; init; }
    [Save("amount")]
    public int Amount { get; init; }

    public StatModifier ToDomain() => new(Stat, Amount);

    public static StatModifierData From(StatModifier modifier) => new()
    {
        Stat = modifier.Stat,
        Amount = modifier.Amount,
    };
}

[Meta, Id("status_resistance_data")]
public partial record StatusResistanceData
{
    [Save("status_id")]
    public string StatusId { get; init; } = "";
    [Save("value")]
    public double Value { get; init; }
}

[Meta, Id("damage_affinity_data")]
public partial record DamageAffinityData
{
    [Save("damage_type")]
    public DamageType DamageType { get; init; }
    [Save("value")]
    public double Value { get; init; }
}
