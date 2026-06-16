namespace Labyrinth;

using System.Collections.Generic;

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

internal sealed record ReactiveEffectInvocation(
    RuntimeReactiveEffect ReactiveEffect,
    ReactiveEffectEvent Event
);

internal abstract record BattleOperation;

internal sealed record CueOperation(IReadOnlyList<BattleCue> Cues)
    : BattleOperation;

internal sealed record ExecuteActionOperation(BattleCommand Command)
    : BattleOperation;

internal sealed record WindowOperation(ReactiveEffectEvent Event)
    : BattleOperation;

internal sealed record DamageOperation(
    EffectContext Context,
    DamageSpec Spec
) : BattleOperation;

internal sealed record HealOperation(
    EffectContext Context,
    int Amount
) : BattleOperation;

internal sealed record ModifyResourceOperation(
    EffectContext Context,
    BattleResource Resource,
    int Amount
) : BattleOperation;

internal sealed record ApplyStatusOperation(
    EffectContext Context,
    ApplyStatusEffectDefinition Effect
) : BattleOperation;

internal sealed record RemoveStatusOperation(
    EffectContext Context,
    StatusId StatusId
) : BattleOperation;

internal sealed record RegisterReactiveEffectOperation(
    EffectContext Context,
    ReactiveEffectId ReactiveEffectId
) : BattleOperation;

internal sealed record UnregisterStatusReactiveEffectsOperation(
    BattlerId OwnerId,
    StatusId StatusId
) : BattleOperation;

internal sealed record ActionEndOperation(EffectContext Context)
    : BattleOperation;

internal sealed record FlushAfterActionReactiveEffectsOperation
    : BattleOperation;

internal sealed record FlushEndTurnReactiveEffectsOperation
    : BattleOperation;

internal sealed record ExpireStatusesOperation : BattleOperation;

internal sealed record DeathCheckOperation(
    BattlerId? SourceId,
    ActionId? ActionId,
    int ReactiveEffectDepth
) : BattleOperation;

internal sealed record FinishTurnOperation : BattleOperation;

internal sealed record CompleteOperation(BattleOutcome Outcome)
    : BattleOperation;
