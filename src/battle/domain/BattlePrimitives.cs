namespace Labyrinth;

using System;
using System.Collections.Generic;

public static class BattleLimits
{
    public const int MaxPlayerBattlers = 5;
    public const int MaxFormationSlotsPerRow = 3;
}

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

public readonly record struct EnemyId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct ReactiveEffectId(string Value)
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
    public bool IsValid =>
      Index is >= 0 and < BattleLimits.MaxFormationSlotsPerRow;
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
