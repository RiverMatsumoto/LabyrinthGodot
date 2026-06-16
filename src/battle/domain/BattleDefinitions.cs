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
    FromWeapon
}

public enum RetargetPolicy
{
    NearestValid,
    Fail,
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

public enum EffectPowerSource
{
    Fixed,
    SourceStatusPower,
    FixedTimesSourceStatusPower,
    SourceStatusPowerTimesStacks,
    FixedTimesSourceStatusPowerTimesStacks,
}

/// <summary>Battle event metadata that can activate a reactive effect.</summary>
public enum ReactiveEffectTrigger
{
    TurnStarted,
    TurnEnded,
    ActionStarted,
    ActionFinished,
    BeforeEffect,
    AfterEffect,
    Damage,
    Healing,
    StatusApplied,
    StatusTriggered,
    StatusRemoved,
    Defeat,
}

/// <summary>Controls when a matched reactive effect enters the resolver queue.</summary>
public enum ReactiveEffectSchedule
{
    Immediate,
    AfterCurrentAction,
    EndOfTurn,
}

/// <summary>Selects the battler affected by reactive effect effects.</summary>
public enum ReactiveEffectTargetPolicy
{
    Owner,
    EventSource,
    EventTarget,
}

/// <summary>Tests whether the reactive effect owner participated in an event.</summary>
public enum ReactiveEffectOwnerRelation
{
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

/// <summary>
/// Multiplies an effect amount by the source status's current stack count.
/// </summary>
public sealed record StatusStackScaleDefinition(StatusId StatusId);

public sealed record DamageEffectDefinition(
    DamageSpec Spec,
    string AnimationId = "",
    StatusStackScaleDefinition? Scale = null,
    EffectPowerSource PowerSource = EffectPowerSource.Fixed
) : BattleEffectDefinition;

public sealed record HealEffectDefinition(
    int Amount,
    string AnimationId = "",
    StatusStackScaleDefinition? Scale = null
) : BattleEffectDefinition;

public sealed record ModifyResourceEffectDefinition(
    BattleResource Resource,
    int Amount,
    StatusStackScaleDefinition? Scale = null
) : BattleEffectDefinition;

public sealed record ApplyStatusEffectDefinition(
    StatusId StatusId,
    int Stacks = 1,
    int Duration = 0,
    double BaseChance = 1.0,
    double Power = 0
) : BattleEffectDefinition;

public sealed record RemoveStatusEffectDefinition(StatusId StatusId)
    : BattleEffectDefinition;

public sealed record PlayAnimationEffectDefinition(
    string AnimationId,
    bool Wait = true
) : BattleEffectDefinition;

public sealed record WaitEffectDefinition(double Seconds)
    : BattleEffectDefinition;

public sealed record RegisterReactiveEffectEffectDefinition(ReactiveEffectId ReactiveEffectId)
    : BattleEffectDefinition;

/// <summary>Base contract for AND-combined reactive effect predicates.</summary>
public abstract record ReactiveEffectConditionDefinition;

public sealed record OwnerHasStatusConditionDefinition(
    StatusId StatusId,
    int MinimumStacks = 1
) : ReactiveEffectConditionDefinition;

public sealed record TriggerActionConditionDefinition(ActionId ActionId)
    : ReactiveEffectConditionDefinition;

public sealed record TriggerStatusConditionDefinition(StatusId StatusId)
    : ReactiveEffectConditionDefinition;

public sealed record OwnerRelationConditionDefinition(
    ReactiveEffectOwnerRelation Relation
) : ReactiveEffectConditionDefinition;

/// <summary>Compiled catalog reactive effect shared by authored references.</summary>
public sealed record ReactiveEffectDefinition(
    ReactiveEffectId Id,
    ReactiveEffectTrigger Trigger,
    ReactiveEffectSchedule Schedule,
    ReactiveEffectTargetPolicy TargetPolicy,
    int Priority,
    IReadOnlyList<ReactiveEffectConditionDefinition> Conditions,
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

/// <summary>
/// Data-driven status behavior and reactive effects; runtime stacks are battle state.
/// </summary>
public sealed record StatusDefinition(
    StatusId Id,
    string Name,
    bool PreventsAction,
    int DefaultDuration,
    int MaxStacks = 1,
    IReadOnlyList<ReactiveEffectId>? ReactiveEffectIdList = null
)
{
    public IReadOnlyList<ReactiveEffectId> ReactiveEffectIds => ReactiveEffectIdList ?? [];
}

/// <summary>
/// Reusable enemy data that is independent from encounter placement.
/// </summary>
public sealed record BattleEnemyDefinition(
    EnemyId Id,
    string Name,
    BattleStats Stats,
    int Hp,
    int Tp,
    IReadOnlyList<ActionId> ActionIds,
    IReadOnlyList<ReactiveEffectId>? ReactiveEffectIdList = null,
    IReadOnlyDictionary<StatusId, double>? StatusResistances = null,
    IReadOnlyDictionary<StatusId, double>? StatusWeaknesses = null,
    IReadOnlyDictionary<DamageType, double>? DamageTypeResistances = null,
    IReadOnlyDictionary<DamageType, double>? DamageTypeWeaknesses = null
)
{
    public IReadOnlyList<ReactiveEffectId> ReactiveEffectIds => ReactiveEffectIdList ?? [];
    public IReadOnlyDictionary<StatusId, double> StatusResistanceValues =>
        StatusResistances ?? new Dictionary<StatusId, double>();
    public IReadOnlyDictionary<StatusId, double> StatusWeaknessValues =>
        StatusWeaknesses ?? new Dictionary<StatusId, double>();
    public IReadOnlyDictionary<DamageType, double> DamageResistanceValues =>
        DamageTypeResistances ?? new Dictionary<DamageType, double>();
    public IReadOnlyDictionary<DamageType, double> DamageWeaknessValues =>
        DamageTypeWeaknesses ?? new Dictionary<DamageType, double>();
}

/// <summary>
/// One encounter-specific enemy instance, identity, and formation position.
/// </summary>
public sealed record BattleEnemyPlacement(
    BattlerId BattlerId,
    PartyPosition Position,
    BattleEnemyDefinition Enemy
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
    IReadOnlyList<ReactiveEffectId>? ReactiveEffectIdList = null,
    IReadOnlyDictionary<StatusId, double>? StatusResistances = null,
    IReadOnlyDictionary<StatusId, double>? StatusWeaknesses = null,
    IReadOnlyDictionary<DamageType, double>? DamageTypeResistances = null,
    IReadOnlyDictionary<DamageType, double>? DamageTypeWeaknesses = null
)
{
    public IReadOnlyList<ReactiveEffectId> ReactiveEffectIds => ReactiveEffectIdList ?? [];
    public IReadOnlyDictionary<StatusId, double> StatusResistanceValues =>
        StatusResistances ?? new Dictionary<StatusId, double>();
    public IReadOnlyDictionary<StatusId, double> StatusWeaknessValues =>
        StatusWeaknesses ?? new Dictionary<StatusId, double>();
    public IReadOnlyDictionary<DamageType, double> DamageResistanceValues =>
        DamageTypeResistances ?? new Dictionary<DamageType, double>();
    public IReadOnlyDictionary<DamageType, double> DamageWeaknessValues =>
        DamageTypeWeaknesses ?? new Dictionary<DamageType, double>();

    public static BattleBattlerSeed FromEnemy(
        BattleEnemyPlacement placement
    ) => new(
        Id: placement.BattlerId,
        Name: placement.Enemy.Name,
        Team: BattleTeam.Enemy,
        Position: placement.Position,
        Stats: placement.Enemy.Stats,
        Hp: placement.Enemy.Hp,
        Tp: placement.Enemy.Tp,
        ActionIds: placement.Enemy.ActionIds,
        ReactiveEffectIdList: placement.Enemy.ReactiveEffectIds,
        StatusResistances: placement.Enemy.StatusResistanceValues,
        StatusWeaknesses: placement.Enemy.StatusWeaknessValues,
        DamageTypeResistances: placement.Enemy.DamageResistanceValues,
        DamageTypeWeaknesses: placement.Enemy.DamageWeaknessValues
    );
}

public sealed record BattleSetup(
    EncounterId EncounterId,
    int Seed,
    IReadOnlyList<BattleBattlerSeed> Players,
    IReadOnlyList<BattleBattlerSeed> Enemies,
    BattleReward Reward
);

public sealed class BattleCatalog
{
    private readonly Dictionary<ActionId, BattleActionDefinition> _actions;
    private readonly Dictionary<StatusId, StatusDefinition> _statuses;
    private readonly Dictionary<ReactiveEffectId, ReactiveEffectDefinition> _reactiveEffects;

    public BattleCatalog(
        IEnumerable<BattleActionDefinition> actions,
        IEnumerable<StatusDefinition> statuses,
        IEnumerable<ReactiveEffectDefinition>? reactiveEffects = null
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
        _reactiveEffects = ToUniqueDictionary(
            reactiveEffects ?? [],
            reactiveEffect => reactiveEffect.Id,
            "reactive effect"
        );
        ValidateReferences();
    }

    public IReadOnlyCollection<BattleActionDefinition> Actions =>
        _actions.Values;
    public IReadOnlyCollection<StatusDefinition> Statuses =>
        _statuses.Values;
    public IReadOnlyCollection<ReactiveEffectDefinition> ReactiveEffects =>
        _reactiveEffects.Values;

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

    public ReactiveEffectDefinition GetReactiveEffect(ReactiveEffectId id) =>
        _reactiveEffects.TryGetValue(id, out var reactiveEffect)
            ? reactiveEffect
            : throw new KeyNotFoundException($"Unknown reactive effect '{id}'.");

    public bool TryGetReactiveEffect(
        ReactiveEffectId id,
        out ReactiveEffectDefinition reactiveEffect
    ) => _reactiveEffects.TryGetValue(id, out reactiveEffect!);

    private void ValidateReferences()
    {
        foreach (var action in _actions.Values)
        {
            ValidateEffects(action.Effects, $"action '{action.Id}'");
        }
        foreach (var status in _statuses.Values)
        {
            foreach (var reactiveEffectId in status.ReactiveEffectIds)
            {
                RequireReactiveEffect(reactiveEffectId, $"status '{status.Id}'");
            }
        }
        foreach (var reactiveEffect in _reactiveEffects.Values)
        {
            ValidateConditions(
                reactiveEffect.Conditions,
                $"reactive effect '{reactiveEffect.Id}'"
            );
            ValidateEffects(reactiveEffect.Effects, $"reactive effect '{reactiveEffect.Id}'");
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
                case RegisterReactiveEffectEffectDefinition register:
                    RequireReactiveEffect(register.ReactiveEffectId, owner);
                    break;
                case DamageEffectDefinition { Scale: { } scale }:
                    RequireStatus(scale.StatusId, owner);
                    break;
                case HealEffectDefinition { Scale: { } scale }:
                    RequireStatus(scale.StatusId, owner);
                    break;
                case ModifyResourceEffectDefinition { Scale: { } scale }:
                    RequireStatus(scale.StatusId, owner);
                    break;
            }
        }
    }

    private void ValidateConditions(
        IEnumerable<ReactiveEffectConditionDefinition> conditions,
        string owner
    )
    {
        foreach (var condition in conditions)
        {
            switch (condition)
            {
                case OwnerHasStatusConditionDefinition status:
                    RequireStatus(status.StatusId, owner);
                    break;
                case TriggerStatusConditionDefinition status:
                    RequireStatus(status.StatusId, owner);
                    break;
                case TriggerActionConditionDefinition action
                    when !_actions.ContainsKey(action.ActionId):
                    throw new InvalidOperationException(
                        $"{owner} references unknown action "
                            + $"'{action.ActionId}'."
                    );
            }
        }
    }

    private void RequireStatus(StatusId id, string owner)
    {
        if (!_statuses.ContainsKey(id))
        {
            throw new InvalidOperationException(
                $"{owner} references unknown status '{id}'."
            );
        }
    }

    private void RequireReactiveEffect(ReactiveEffectId id, string owner)
    {
        if (!_reactiveEffects.ContainsKey(id))
        {
            throw new InvalidOperationException(
                $"{owner} references unknown reactive effect '{id}'."
            );
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
    public static readonly EncounterId DefaultEncounterId =
        new("floor_1_squirrel");
    public static readonly ActionId BasicAttackId = new("basic_attack");
    public static readonly ActionId FireId = new("fire");
    public static readonly ActionId PoisonStrikeId = new("poison_strike");
    public static readonly ActionId HealId = new("heal");
    public static readonly StatusId PoisonId = new("poison");
    public static readonly StatusId StunId = new("stun");
    public static readonly StatusId RegenId = new("regen");
    public static readonly ReactiveEffectId PoisonTickReactiveEffectId =
        new("poison_tick");
    public static readonly ReactiveEffectId RegenTickReactiveEffectId =
        new("regen_tick");
    public static readonly ReactiveEffectId ToxicRecoveryReactiveEffectId =
        new("toxic_recovery");

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
                        BaseChance: 0.75,
                        Power: 10
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
                PreventsAction: false,
                DefaultDuration: 3,
                MaxStacks: 3,
                ReactiveEffectIdList: [PoisonTickReactiveEffectId]
            ),
            new StatusDefinition(
                StunId,
                "Stun",
                PreventsAction: true,
                DefaultDuration: 1
            ),
            new StatusDefinition(
                RegenId,
                "Regen",
                PreventsAction: false,
                DefaultDuration: 3,
                MaxStacks: 3,
                ReactiveEffectIdList: [RegenTickReactiveEffectId]
            ),
        ],
        [
            new ReactiveEffectDefinition(
                PoisonTickReactiveEffectId,
                ReactiveEffectTrigger.TurnEnded,
                ReactiveEffectSchedule.EndOfTurn,
                ReactiveEffectTargetPolicy.Owner,
                Priority: 10,
                Conditions: [],
                Effects:
                [
                    new DamageEffectDefinition(
                        new DamageSpec(
                            DamageType.True,
                            DamageMode.Fixed,
                            10
                        ),
                        PowerSource:
                            EffectPowerSource.FixedTimesSourceStatusPower
                    ),
                ]
            ),
            new ReactiveEffectDefinition(
                RegenTickReactiveEffectId,
                ReactiveEffectTrigger.TurnEnded,
                ReactiveEffectSchedule.EndOfTurn,
                ReactiveEffectTargetPolicy.Owner,
                Priority: 0,
                Conditions: [],
                Effects:
                [
                    new HealEffectDefinition(
                        5,
                        Scale: new StatusStackScaleDefinition(RegenId)
                    ),
                ]
            ),
            new ReactiveEffectDefinition(
                ToxicRecoveryReactiveEffectId,
                ReactiveEffectTrigger.TurnEnded,
                ReactiveEffectSchedule.EndOfTurn,
                ReactiveEffectTargetPolicy.Owner,
                Priority: 0,
                Conditions:
                [
                    new OwnerHasStatusConditionDefinition(PoisonId),
                ],
                Effects:
                [
                    new HealEffectDefinition(
                        5,
                        Scale: new StatusStackScaleDefinition(PoisonId)
                    ),
                ]
            ),
        ]
    );
}
