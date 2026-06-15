namespace Labyrinth;

using System.Collections.Generic;

internal sealed record EffectContext(
    BattlerId SourceId,
    IReadOnlyList<BattlerId> TargetIds,
    BattleRange Range,
    ActionId? ActionId,
    int ReactionDepth,
    StatusId? StatusId,
    int StatusStacks
);

internal sealed record ReactionEvent(
    long CauseId,
    ReactionTrigger Trigger,
    BattlerId? SourceId = null,
    BattlerId? TargetId = null,
    ActionId? ActionId = null,
    StatusId? StatusId = null,
    int StatusStacks = 0,
    int Depth = 0
);

internal sealed record ReactionInvocation(
    RuntimeReaction Reaction,
    ReactionEvent Event
);

internal abstract record BattleOperation;

internal sealed record CueOperation(IReadOnlyList<BattleCue> Cues)
    : BattleOperation;

internal sealed record ExecuteActionOperation(BattleCommand Command)
    : BattleOperation;

internal sealed record WindowOperation(ReactionEvent Event)
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

internal sealed record RegisterReactionOperation(
    EffectContext Context,
    ReactionId ReactionId
) : BattleOperation;

internal sealed record UnregisterStatusReactionsOperation(
    BattlerId OwnerId,
    StatusId StatusId
) : BattleOperation;

internal sealed record ActionEndOperation(EffectContext Context)
    : BattleOperation;

internal sealed record FlushAfterActionReactionsOperation
    : BattleOperation;

internal sealed record FlushEndTurnReactionsOperation
    : BattleOperation;

internal sealed record ExpireStatusesOperation : BattleOperation;

internal sealed record DeathCheckOperation(
    BattlerId? SourceId,
    ActionId? ActionId,
    int ReactionDepth
) : BattleOperation;

internal sealed record FinishTurnOperation : BattleOperation;

internal sealed record CompleteOperation(BattleOutcome Outcome)
    : BattleOperation;
