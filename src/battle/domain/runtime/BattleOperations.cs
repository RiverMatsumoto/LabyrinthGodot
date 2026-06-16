namespace Labyrinth;

using System.Collections.Generic;

/// <summary>
/// Carries source, target, action, status, and depth metadata for effect execution.
/// </summary>
internal sealed record EffectContext(
    BattlerId SourceId,
    IReadOnlyList<BattlerId> TargetIds,
    BattleRange Range,
    ActionId? ActionId,
    int ReactiveEffectDepth,
    StatusId? StatusId,
    int StatusStacks,
    double StatusPower
);

/// <summary>Describes a trigger point that can activate reactive effects.</summary>
internal sealed record ReactiveEffectEvent(
    long CauseId,
    ReactiveEffectTrigger Trigger,
    BattlerId? SourceId = null,
    BattlerId? TargetId = null,
    ActionId? ActionId = null,
    StatusId? StatusId = null,
    int StatusStacks = 0,
    double StatusPower = 0,
    int Depth = 0
);

/// <summary>Pairs a registered reactive effect with the event that matched it.</summary>
internal sealed record ReactiveEffectInvocation(
    RuntimeReactiveEffect ReactiveEffect,
    ReactiveEffectEvent Event
);

/// <summary>Base queued unit of battle resolution work.</summary>
internal abstract record BattleOperation;

/// <summary>Requests presentation playback before resolution continues.</summary>
internal sealed record VisualCueOperation(IReadOnlyList<BattleCue> Cues)
    : BattleOperation;

/// <summary>Expands a submitted command into ordered effect operations.</summary>
internal sealed record ExecuteActionOperation(BattleCommand Command)
    : BattleOperation;

/// <summary>Triggers matching reactive effects for a battle event.</summary>
internal sealed record TriggerReactiveEffectsOperation(ReactiveEffectEvent Event)
    : BattleOperation;

/// <summary>Applies damage to the current effect targets.</summary>
internal sealed record DamageOperation(
    EffectContext Context,
    DamageSpec Spec
) : BattleOperation;

/// <summary>Restores HP to the current effect targets.</summary>
internal sealed record HealOperation(
    EffectContext Context,
    int Amount
) : BattleOperation;

/// <summary>Changes HP or TP by a signed amount.</summary>
internal sealed record ModifyResourceOperation(
    EffectContext Context,
    BattleResource Resource,
    int Amount
) : BattleOperation;

/// <summary>Applies or refreshes a status on the current effect targets.</summary>
internal sealed record ApplyStatusOperation(
    EffectContext Context,
    ApplyStatusEffectDefinition Effect
) : BattleOperation;

/// <summary>Removes a status from the current effect targets.</summary>
internal sealed record RemoveStatusOperation(
    EffectContext Context,
    StatusId StatusId
) : BattleOperation;

/// <summary>Registers a reactive effect on the effect source.</summary>
internal sealed record RegisterReactiveEffectOperation(
    EffectContext Context,
    ReactiveEffectId ReactiveEffectId
) : BattleOperation;

/// <summary>Removes reactive effects owned by an expiring or removed status.</summary>
internal sealed record UnregisterStatusReactiveEffectsOperation(
    BattlerId OwnerId,
    StatusId StatusId
) : BattleOperation;

/// <summary>Marks action completion and schedules after-action reactive effects.</summary>
internal sealed record ActionEndOperation(EffectContext Context)
    : BattleOperation;

/// <summary>Flushes reactive effects deferred until the current action ends.</summary>
internal sealed record FlushAfterActionReactiveEffectsOperation
    : BattleOperation;

/// <summary>Flushes reactive effects deferred until the end of the turn.</summary>
internal sealed record FlushEndTurnReactiveEffectsOperation
    : BattleOperation;

/// <summary>Ticks status duration and removes expired statuses.</summary>
internal sealed record ExpireStatusesOperation : BattleOperation;

/// <summary>Checks defeated units and emits defeat follow-up operations.</summary>
internal sealed record DeathCheckOperation(
    BattlerId? SourceId,
    ActionId? ActionId,
    int ReactiveEffectDepth
) : BattleOperation;

/// <summary>Ends the current turn and returns to command selection.</summary>
internal sealed record FinishTurnOperation : BattleOperation;

/// <summary>Completes battle resolution with a terminal outcome.</summary>
internal sealed record CompleteOperation(BattleOutcome Outcome)
    : BattleOperation;
