namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

public readonly record struct BattlerId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ActionId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct StatusId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct EncounterId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct EquipmentId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

/// <summary>Identifies reusable enemy authoring data.</summary>
public readonly record struct EnemyId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

/// <summary>Identifies a reaction in the battle content catalog.</summary>
public readonly record struct ReactionId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public enum PartyRow
{
    Front,
    Back,
}

public readonly record struct PartyPosition(PartyRow Row, int Index)
{
    public bool IsValid => Index is >= 0 and < PartyRepo.MaxMembersPerRow;
}

public enum BattleStat
{
    MaxHp,
    MaxTp,
    Strength,
    Technique,
    Agility,
    Vitality,
    Wisdom,
    Luck,
    Attack,
    Defense,
}

public readonly record struct StatModifier(BattleStat Stat, int Amount);

public sealed record BattleStats(
    int MaxHp,
    int MaxTp,
    int Strength,
    int Technique,
    int Agility,
    int Vitality,
    int Wisdom,
    int Luck,
    int Attack,
    int Defense
)
{
    public static BattleStats Default => new(
        MaxHp: 100,
        MaxTp: 20,
        Strength: 10,
        Technique: 10,
        Agility: 10,
        Vitality: 10,
        Wisdom: 10,
        Luck: 10,
        Attack: 0,
        Defense: 0
    );

    public BattleStats Apply(IEnumerable<StatModifier> modifiers)
    {
        var values = new Dictionary<BattleStat, int>
        {
            [BattleStat.MaxHp] = MaxHp,
            [BattleStat.MaxTp] = MaxTp,
            [BattleStat.Strength] = Strength,
            [BattleStat.Technique] = Technique,
            [BattleStat.Agility] = Agility,
            [BattleStat.Vitality] = Vitality,
            [BattleStat.Wisdom] = Wisdom,
            [BattleStat.Luck] = Luck,
            [BattleStat.Attack] = Attack,
            [BattleStat.Defense] = Defense,
        };

        foreach (var modifier in modifiers)
        {
            values[modifier.Stat] += modifier.Amount;
        }

        return new BattleStats(
            Math.Max(1, values[BattleStat.MaxHp]),
            Math.Max(0, values[BattleStat.MaxTp]),
            values[BattleStat.Strength],
            values[BattleStat.Technique],
            values[BattleStat.Agility],
            values[BattleStat.Vitality],
            values[BattleStat.Wisdom],
            values[BattleStat.Luck],
            values[BattleStat.Attack],
            values[BattleStat.Defense]
        );
    }
}

public sealed class PartyMember
{
    public required BattlerId Id { get; init; }
    public required string Name { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public required BattleStats BaseStats { get; set; }
    public int Hp { get; set; }
    public int Tp { get; set; }
    public List<ActionId> LearnedActions { get; } = [];
    public List<EquipmentId> Equipment { get; } = [];
    public List<StatModifier> EquipmentModifiers { get; } = [];
    public Dictionary<StatusId, double> StatusResistances { get; } = [];
    public Dictionary<StatusId, double> StatusWeakness { get; } = [];
    public Dictionary<DamageType, double> DamageTypeResistances { get; } = [];
    public Dictionary<DamageType, double> DamageTypeWeaknesses { get; } = [];
    public List<ReactionId> PassiveReactionIds { get; } = [];

    public BattleStats EffectiveStats => BaseStats.Apply(EquipmentModifiers);
    public bool IsAlive => Hp > 0;

    public PartyMember Clone()
    {
        var clone = new PartyMember
        {
            Id = Id,
            Name = Name,
            Level = Level,
            Experience = Experience,
            BaseStats = BaseStats,
            Hp = Hp,
            Tp = Tp,
        };
        clone.LearnedActions.AddRange(LearnedActions);
        clone.Equipment.AddRange(Equipment);
        clone.EquipmentModifiers.AddRange(EquipmentModifiers);
        foreach (var pair in StatusResistances)
        {
            clone.StatusResistances[pair.Key] = pair.Value;
        }
        foreach (var pair in StatusWeakness)
        {
            clone.StatusWeakness[pair.Key] = pair.Value;
        }
        foreach (var pair in DamageTypeResistances)
        {
            clone.DamageTypeResistances[pair.Key] = pair.Value;
        }
        foreach (var pair in DamageTypeWeaknesses)
        {
            clone.DamageTypeWeaknesses[pair.Key] = pair.Value;
        }
        clone.PassiveReactionIds.AddRange(PassiveReactionIds);
        return clone;
    }
}

public readonly record struct PartyMemberEntry(
    PartyMember Member,
    PartyPosition Position
);

public interface IPartyRepo : IDisposable
{
    int Count { get; }
    IReadOnlyList<PartyMemberEntry> Members { get; }

    bool TryAdd(PartyMember member, PartyPosition position);
    bool TryRemove(BattlerId id);
    bool TryMove(BattlerId id, PartyPosition position);
    bool TryGet(BattlerId id, out PartyMember member);
    bool TryGetPosition(BattlerId id, out PartyPosition position);
    void ApplyBattleVitals(
        IReadOnlyDictionary<BattlerId, (int Hp, int Tp)> vitals
    );
    PartyData ToData();
    void Load(PartyData data);
}

public sealed class PartyRepo : IPartyRepo
{
    public const int MaxMembers = 5;
    public const int MaxMembersPerRow = 3;

    private readonly Dictionary<BattlerId, PartyMember> _members = [];
    private readonly Dictionary<BattlerId, PartyPosition> _positions = [];
    private bool _disposed;

    public int Count => _members.Count;

    public IReadOnlyList<PartyMemberEntry> Members =>
        _members.Values
            .Select(member => new PartyMemberEntry(
                member,
                _positions[member.Id]
            ))
            .OrderBy(entry => entry.Position.Row)
            .ThenBy(entry => entry.Position.Index)
            .ToArray();

    public bool TryAdd(PartyMember member, PartyPosition position)
    {
        ArgumentNullException.ThrowIfNull(member);
        ThrowIfDisposed();

        if (
            member.Id.IsEmpty
            || !position.IsValid
            || _members.Count >= MaxMembers
            || _members.ContainsKey(member.Id)
            || _positions.ContainsValue(position)
        )
        {
            return false;
        }

        NormalizeVitals(member);
        _members.Add(member.Id, member);
        _positions.Add(member.Id, position);
        return true;
    }

    public bool TryRemove(BattlerId id)
    {
        ThrowIfDisposed();
        _positions.Remove(id);
        return _members.Remove(id);
    }

    public bool TryMove(BattlerId id, PartyPosition position)
    {
        ThrowIfDisposed();
        if (
            !position.IsValid
            || !_members.ContainsKey(id)
            || _positions.Any(pair =>
                pair.Key != id && pair.Value == position)
        )
        {
            return false;
        }

        _positions[id] = position;
        return true;
    }

    public bool TryGet(BattlerId id, out PartyMember member) =>
        _members.TryGetValue(id, out member!);

    public bool TryGetPosition(BattlerId id, out PartyPosition position) =>
        _positions.TryGetValue(id, out position);

    public void ApplyBattleVitals(
        IReadOnlyDictionary<BattlerId, (int Hp, int Tp)> vitals
    )
    {
        ThrowIfDisposed();
        foreach (var pair in vitals)
        {
            if (!_members.TryGetValue(pair.Key, out var member))
            {
                continue;
            }

            var stats = member.EffectiveStats;
            member.Hp = Math.Clamp(pair.Value.Hp, 0, stats.MaxHp);
            member.Tp = Math.Clamp(pair.Value.Tp, 0, stats.MaxTp);
        }
    }

    public PartyData ToData() => PartyData.From(this);

    public void Load(PartyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ThrowIfDisposed();
        _members.Clear();
        _positions.Clear();

        foreach (var entry in data.Members)
        {
            TryAdd(entry.ToDomain(), new PartyPosition(entry.Row, entry.Slot));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _members.Clear();
        _positions.Clear();
        _disposed = true;
    }

    private static void NormalizeVitals(PartyMember member)
    {
        var stats = member.EffectiveStats;
        member.Hp = Math.Clamp(member.Hp, 0, stats.MaxHp);
        member.Tp = Math.Clamp(member.Tp, 0, stats.MaxTp);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
