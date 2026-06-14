namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleTeam
{
    Player,
    Enemy,
}

public enum BattleTargetRule
{
    Self,
    SingleAlly,
    SingleEnemy,
    RowAllies,
    RowEnemies,
    AllAllies,
    AllEnemies,
}

public enum BattleRange
{
    None,
    Melee,
    Ranged,
}

public enum RetargetPolicy
{
    Fail,
    NearestValid,
}

public enum DamageType
{
    Cut,
    Bash,
    Stab,
    Fire,
    Ice,
    Lightning,
    True,
    FromWeapon,
}

public enum DamageMode
{
    FromStats,
    Fixed,
}

public enum BattleResource
{
    Hp,
    Tp,
}

public enum StatusBehavior
{
    None,
    Poison,
    Stun,
    Regen,
}

public enum ReactionWindow
{
    TurnStart,
    TurnEnd,
    ActionStart,
    ActionEnd,
    BeforeEffect,
    AfterEffect,
    Damage,
    Healing,
    StatusApplied,
    Defeat,
}

public enum ReactionInsertionPolicy
{
    BeforeNextEffect,
    AfterCurrentAction,
    EndOfTurn,
}

public enum ReactionTargetPolicy
{
    Owner,
    EventSource,
    EventTarget,
}

public sealed record DamageSpec(
    DamageType Type,
    DamageMode Mode,
    double Power,
    bool CanCrit = false,
    double CritMultiplier = 1.8
);

public abstract record BattleEffectDefinition;

public sealed record DamageEffectDefinition(
    DamageSpec Spec,
    string AnimationId = ""
) : BattleEffectDefinition;

public sealed record HealEffectDefinition(
    int Amount,
    string AnimationId = ""
) : BattleEffectDefinition;

public sealed record ModifyResourceEffectDefinition(
    BattleResource Resource,
    int Amount
) : BattleEffectDefinition;

public sealed record ApplyStatusEffectDefinition(
    StatusId StatusId,
    int Stacks = 1,
    int Duration = 0,
    double BaseChance = 1.0
) : BattleEffectDefinition;

public sealed record RemoveStatusEffectDefinition(StatusId StatusId)
    : BattleEffectDefinition;

public sealed record PlayAnimationEffectDefinition(
    string AnimationId,
    bool Wait = true
) : BattleEffectDefinition;

public sealed record WaitEffectDefinition(double Seconds)
    : BattleEffectDefinition;

public sealed record RegisterReactionEffectDefinition(
    ReactionDefinition Reaction
) : BattleEffectDefinition;

public sealed record ReactionDefinition(
    string Id,
    ReactionWindow Window,
    ReactionInsertionPolicy InsertionPolicy,
    ReactionTargetPolicy TargetPolicy,
    int Priority,
    IReadOnlyList<BattleEffectDefinition> Effects,
    int Uses = -1
);

public sealed record BattleActionDefinition(
    ActionId Id,
    string Name,
    BattleTargetRule TargetRule,
    IReadOnlyList<BattleEffectDefinition> Effects,
    int TpCost = 0,
    int Priority = 0,
    BattleRange Range = BattleRange.None,
    RetargetPolicy RetargetPolicy = RetargetPolicy.Fail
);

public sealed record StatusDefinition(
    StatusId Id,
    string Name,
    StatusBehavior Behavior,
    int DefaultDuration,
    int MaxStacks = 1,
    int PowerPerStack = 0
);

public sealed record BattleReward(
    int Experience = 0,
    int Currency = 0,
    IReadOnlyList<EquipmentId>? ItemIds = null
)
{
    public IReadOnlyList<EquipmentId> Items => ItemIds ?? [];
}

public sealed record EquipmentDefinition(
    EquipmentId Id,
    string Name,
    IReadOnlyList<StatModifier> Modifiers
);

public sealed record EncounterDefinition(
    EncounterId Id,
    IReadOnlyList<BattleBattlerSeed> Enemies,
    BattleReward Reward
);

public sealed record BattleBattlerSeed(
    BattlerId Id,
    string Name,
    BattleTeam Team,
    PartyPosition Position,
    BattleStats Stats,
    int Hp,
    int Tp,
    IReadOnlyList<ActionId> ActionIds,
    IReadOnlyDictionary<StatusId, double>? StatusResistances = null
)
{
    public IReadOnlyDictionary<StatusId, double> Resistances =>
        StatusResistances
        ?? new Dictionary<StatusId, double>();

    public static BattleBattlerSeed FromParty(PartyMemberEntry entry) => new(
        entry.Member.Id,
        entry.Member.Name,
        BattleTeam.Player,
        entry.Position,
        entry.Member.EffectiveStats,
        entry.Member.Hp,
        entry.Member.Tp,
        entry.Member.LearnedActions.ToArray(),
        new Dictionary<StatusId, double>(
            entry.Member.StatusResistances
        )
    );
}

public sealed record BattleSetup(
    EncounterId EncounterId,
    int Seed,
    GameMode ReturnMode,
    IReadOnlyList<BattleBattlerSeed> Players,
    IReadOnlyList<BattleBattlerSeed> Enemies,
    BattleReward Reward
);

public sealed class BattleCatalog
{
    private readonly Dictionary<ActionId, BattleActionDefinition> _actions;
    private readonly Dictionary<StatusId, StatusDefinition> _statuses;

    public BattleCatalog(
        IEnumerable<BattleActionDefinition> actions,
        IEnumerable<StatusDefinition> statuses
    )
    {
        _actions = ToUniqueDictionary(
            actions,
            action => action.Id,
            "action"
        );
        _statuses = ToUniqueDictionary(
            statuses,
            status => status.Id,
            "status"
        );
        ValidateReferences();
    }

    public IReadOnlyCollection<BattleActionDefinition> Actions =>
        _actions.Values;
    public IReadOnlyCollection<StatusDefinition> Statuses =>
        _statuses.Values;

    public BattleActionDefinition GetAction(ActionId id) =>
        _actions.TryGetValue(id, out var action)
            ? action
            : throw new KeyNotFoundException($"Unknown action '{id}'.");

    public bool TryGetAction(
        ActionId id,
        out BattleActionDefinition action
    ) => _actions.TryGetValue(id, out action!);

    public StatusDefinition GetStatus(StatusId id) =>
        _statuses.TryGetValue(id, out var status)
            ? status
            : throw new KeyNotFoundException($"Unknown status '{id}'.");

    public bool TryGetStatus(
        StatusId id,
        out StatusDefinition status
    ) => _statuses.TryGetValue(id, out status!);

    private void ValidateReferences()
    {
        foreach (var action in _actions.Values)
        {
            ValidateEffects(action.Effects, $"action '{action.Id}'");
        }
    }

    private void ValidateEffects(
        IEnumerable<BattleEffectDefinition> effects,
        string owner
    )
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case ApplyStatusEffectDefinition apply
                    when !_statuses.ContainsKey(apply.StatusId):
                    throw new InvalidOperationException(
                        $"{owner} references unknown status "
                            + $"'{apply.StatusId}'."
                    );
                case RemoveStatusEffectDefinition remove
                    when !_statuses.ContainsKey(remove.StatusId):
                    throw new InvalidOperationException(
                        $"{owner} references unknown status "
                            + $"'{remove.StatusId}'."
                    );
                case RegisterReactionEffectDefinition register:
                    ValidateEffects(
                        register.Reaction.Effects,
                        $"reaction '{register.Reaction.Id}'"
                    );
                    break;
            }
        }
    }

    private static Dictionary<TKey, TValue> ToUniqueDictionary<TKey, TValue>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> keySelector,
        string kind
    ) where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            var key = keySelector(value);
            if (!result.TryAdd(key, value))
            {
                throw new InvalidOperationException(
                    $"Duplicate {kind} id '{key}'."
                );
            }
        }
        return result;
    }
}

public static class BattleContent
{
    public static readonly ActionId BasicAttackId = new("basic_attack");
    public static readonly ActionId FireId = new("fire");
    public static readonly ActionId PoisonStrikeId = new("poison_strike");
    public static readonly ActionId HealId = new("heal");
    public static readonly StatusId PoisonId = new("poison");
    public static readonly StatusId StunId = new("stun");
    public static readonly StatusId RegenId = new("regen");

    public static BattleCatalog CreateDefaultCatalog() => new(
        [
            new BattleActionDefinition(
                BasicAttackId,
                "Attack",
                BattleTargetRule.SingleEnemy,
                [
                    new DamageEffectDefinition(
                        new DamageSpec(
                            DamageType.Cut,
                            DamageMode.FromStats,
                            12,
                            CanCrit: true
                        ),
                        "cut"
                    ),
                ],
                Range: BattleRange.Melee,
                RetargetPolicy: RetargetPolicy.NearestValid
            ),
            new BattleActionDefinition(
                FireId,
                "Fire",
                BattleTargetRule.SingleEnemy,
                [
                    new DamageEffectDefinition(
                        new DamageSpec(
                            DamageType.Fire,
                            DamageMode.FromStats,
                            18
                        ),
                        "fire"
                    ),
                ],
                TpCost: 4,
                Range: BattleRange.Ranged,
                RetargetPolicy: RetargetPolicy.NearestValid
            ),
            new BattleActionDefinition(
                PoisonStrikeId,
                "Poison Strike",
                BattleTargetRule.SingleEnemy,
                [
                    new DamageEffectDefinition(
                        new DamageSpec(
                            DamageType.Stab,
                            DamageMode.FromStats,
                            8
                        ),
                        "stab"
                    ),
                    new ApplyStatusEffectDefinition(
                        PoisonId,
                        Duration: 3,
                        BaseChance: 0.75
                    ),
                ],
                TpCost: 3,
                Range: BattleRange.Melee,
                RetargetPolicy: RetargetPolicy.NearestValid
            ),
            new BattleActionDefinition(
                HealId,
                "Heal",
                BattleTargetRule.SingleAlly,
                [new HealEffectDefinition(30, "heal")],
                TpCost: 4,
                Range: BattleRange.Ranged,
                RetargetPolicy: RetargetPolicy.NearestValid
            ),
        ],
        [
            new StatusDefinition(
                PoisonId,
                "Poison",
                StatusBehavior.Poison,
                DefaultDuration: 3,
                MaxStacks: 3,
                PowerPerStack: 5
            ),
            new StatusDefinition(
                StunId,
                "Stun",
                StatusBehavior.Stun,
                DefaultDuration: 1
            ),
            new StatusDefinition(
                RegenId,
                "Regen",
                StatusBehavior.Regen,
                DefaultDuration: 3,
                MaxStacks: 3,
                PowerPerStack: 5
            ),
        ]
    );
}
