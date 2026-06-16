namespace Labyrinth;

using System.Collections.Generic;

public readonly record struct CharacterClassId(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public sealed record CharacterClassDefinition(
    CharacterClassId Id,
    string Name,
    BattleStats Stats,
    IReadOnlyList<ActionId> ActionIds,
    IReadOnlyList<ReactiveEffectId> PassiveReactiveEffectIds,
    IReadOnlyDictionary<StatusId, double> StatusResistances,
    IReadOnlyDictionary<StatusId, double> StatusWeaknesses,
    IReadOnlyDictionary<DamageType, double> DamageTypeResistances,
    IReadOnlyDictionary<DamageType, double> DamageTypeWeaknesses
);

public sealed record BattleCharacterDefinition(
    BattlerId Id,
    string Name,
    int Level,
    int Experience,
    CharacterClassDefinition Class
)
{
    public PartyMember CreatePartyMember()
    {
        var member = new PartyMember
        {
            Id = Id,
            Name = Name,
            Level = Level,
            Experience = Experience,
            BaseStats = Class.Stats,
            Hp = Class.Stats.MaxHp,
            Tp = Class.Stats.MaxTp,
        };
        member.LearnedActions.AddRange(Class.ActionIds);
        member.PassiveReactiveEffectIds.AddRange(Class.PassiveReactiveEffectIds);
        foreach (var affinity in Class.StatusResistances)
        {
            member.StatusResistances[affinity.Key] = affinity.Value;
        }
        foreach (var affinity in Class.StatusWeaknesses)
        {
            member.StatusWeakness[affinity.Key] = affinity.Value;
        }
        foreach (var affinity in Class.DamageTypeResistances)
        {
            member.DamageTypeResistances[affinity.Key] = affinity.Value;
        }
        foreach (var affinity in Class.DamageTypeWeaknesses)
        {
            member.DamageTypeWeaknesses[affinity.Key] = affinity.Value;
        }
        return member;
    }
}

public sealed record DebugPartyEntryDefinition(
    BattleCharacterDefinition Character,
    PartyPosition Position
);

public sealed record DebugPartyDefinition(
    IReadOnlyList<DebugPartyEntryDefinition> Entries
);

public sealed class CompiledPartyContent(
    IReadOnlyDictionary<CharacterClassId, CharacterClassDefinition> classes,
    IReadOnlyDictionary<BattlerId, BattleCharacterDefinition> characters,
    DebugPartyDefinition debugParty
)
{
    public IReadOnlyDictionary<CharacterClassId, CharacterClassDefinition>
        Classes { get; } = classes;
    public IReadOnlyDictionary<BattlerId, BattleCharacterDefinition>
        Characters { get; } = characters;
    public DebugPartyDefinition DebugParty { get; } = debugParty;
}
