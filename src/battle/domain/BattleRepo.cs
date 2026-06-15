namespace Labyrinth;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// In-memory implementation of <see cref="IBattleRepo"/>. It resolves commands
/// through an operation queue and pauses only at external interaction
/// boundaries.
/// </summary>
public sealed class BattleRepo : IBattleRepo
{
    public const int MaxReactionDepth = 16;
    public const double BackRowMeleeMultiplier = 0.5;

    private readonly Dictionary<BattlerId, BattleUnit> _units = [];
    private readonly Dictionary<BattlerId, BattleCommand> _playerCommands = [];
    private readonly List<BattlerId> _commandOrder = [];
    private readonly LinkedList<Operation> _operations = [];
    private readonly List<ReactionInvocation> _afterActionReactions = [];
    private readonly List<ReactionInvocation> _endTurnReactions = [];
    private readonly List<RuntimeReaction> _reactions = [];
    private readonly HashSet<(long RegistrationId, long CauseId)>
        _reactionGuards = [];
    private readonly HashSet<BattlerId> _handledDeaths = [];

    private BattleSetup? _setup;
    private IRandomSource _random = new SeededRandomSource(1);
    private long _nextCueBatchId;
    private long _awaitingCueBatchId;
    private long _nextCauseId;
    private long _nextReactionRegistrationId;
    private BattleResult? _result;
    private bool _afterActionFlushStarted;
    private bool _endTurnFlushStarted;
    private bool _disposed;

    public BattleRepo(BattleCatalog catalog)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BattleDomainPhase Phase { get; private set; } =
        BattleDomainPhase.Disabled;
    public BattleCatalog Catalog { get; }
    public int Turn { get; private set; }
    public BattlerId? RequestedPlayerId => FindRequestedPlayer();
    public BattleResult? Result => _result;

    public void Start(BattleSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ThrowIfDisposed();
        Reset();

        if (
            setup.Players.Count is < 1
            or > BattleLimits.MaxPlayerBattlers
        )
        {
            throw new ArgumentException(
                "Battle requires one to five player battlers.",
                nameof(setup)
            );
        }
        if (setup.Enemies.Count == 0)
        {
            throw new ArgumentException(
                "Battle requires at least one enemy.",
                nameof(setup)
            );
        }
        if (!setup.Players.Any(player => player.Hp > 0))
        {
            throw new ArgumentException(
                "Battle requires at least one living player battler.",
                nameof(setup)
            );
        }
        if (!setup.Enemies.Any(enemy => enemy.Hp > 0))
        {
            throw new ArgumentException(
                "Battle requires at least one living enemy battler.",
                nameof(setup)
            );
        }

        _setup = setup;
        _random = new SeededRandomSource(setup.Seed);
        foreach (var seed in setup.Players.Concat(setup.Enemies))
        {
            if (!_units.TryAdd(seed.Id, new BattleUnit(seed)))
            {
                throw new ArgumentException(
                    $"Duplicate battler id '{seed.Id}'.",
                    nameof(setup)
                );
            }

            foreach (var actionId in seed.ActionIds)
            {
                _ = Catalog.GetAction(actionId);
            }
            foreach (var reactionId in seed.ReactionIds)
            {
                _ = Catalog.GetReaction(reactionId);
            }
        }
        foreach (var unit in _units.Values)
        {
            foreach (var reactionId in unit.ReactionIds)
            {
                RegisterRuntimeReaction(unit.Id, reactionId, null);
            }
        }

        Turn = 1;
        Phase = BattleDomainPhase.SelectingCommands;
    }

    public CommandValidationResult ValidateCommand(BattleCommand command)
    {
        ThrowIfDisposed();
        if (Phase != BattleDomainPhase.SelectingCommands)
        {
            return CommandValidationResult.Invalid(
                "Battle is not selecting commands."
            );
        }
        if (
            !_units.TryGetValue(command.ActorId, out var actor)
            || actor.Team != BattleTeam.Player
            || !actor.IsAlive
        )
        {
            return CommandValidationResult.Invalid(
                "Command actor is not a living player battler."
            );
        }
        if (
            !Catalog.TryGetAction(command.ActionId, out var action)
            || !actor.ActionIds.Contains(command.ActionId)
        )
        {
            return CommandValidationResult.Invalid(
                "Battler does not know the requested action."
            );
        }
        if (actor.Tp < action.TpCost)
        {
            return CommandValidationResult.Invalid("Not enough TP.");
        }

        var targets = ResolveTargets(actor, action, command.TargetId);
        return targets.Count > 0
            ? CommandValidationResult.Valid
            : CommandValidationResult.Invalid(
                "The action has no valid targets."
            );
    }

    public bool SubmitCommand(BattleCommand command)
    {
        var validation = ValidateCommand(command);
        if (!validation.IsValid)
        {
            return false;
        }

        if (!_playerCommands.ContainsKey(command.ActorId))
        {
            _commandOrder.Add(command.ActorId);
        }
        _playerCommands[command.ActorId] = command;
        return true;
    }

    public bool UndoLastCommand()
    {
        ThrowIfDisposed();
        if (
            Phase != BattleDomainPhase.SelectingCommands
            || _commandOrder.Count == 0
        )
        {
            return false;
        }

        var id = _commandOrder[^1];
        _commandOrder.RemoveAt(_commandOrder.Count - 1);
        return _playerCommands.Remove(id);
    }

    public IReadOnlyList<BattlerId> GetValidTargets(
        BattlerId actorId,
        ActionId actionId
    )
    {
        ThrowIfDisposed();
        if (
            !_units.TryGetValue(actorId, out var actor)
            || !Catalog.TryGetAction(actionId, out var action)
        )
        {
            return [];
        }

        return GetTargetCandidates(actor, action)
            .Select(unit => unit.Id)
            .ToArray();
    }

    public void BeginResolution(IEnemyCommandPlanner enemyCommandPlanner)
    {
        ArgumentNullException.ThrowIfNull(enemyCommandPlanner);
        ThrowIfDisposed();
        if (Phase != BattleDomainPhase.SelectingCommands)
        {
            throw new InvalidOperationException(
                "Battle is not selecting commands."
            );
        }
        if (FindRequestedPlayer() is not null)
        {
            throw new InvalidOperationException(
                "Every living player requires a command."
            );
        }

        var commands = new List<BattleCommand>(_playerCommands.Values);
        var snapshot = Snapshot();
        foreach (var enemy in OrderedUnits(BattleTeam.Enemy))
        {
            if (!enemy.IsAlive)
            {
                continue;
            }
            var command = enemyCommandPlanner.Plan(
                snapshot,
                enemy.Id,
                Catalog,
                _random
            );
            if (command is not null && IsValidEnemyCommand(command, enemy))
            {
                commands.Add(command);
            }
        }

        var ordered = commands
            .Where(command => _units.ContainsKey(command.ActorId))
            .OrderByDescending(command =>
                Catalog.GetAction(command.ActionId).Priority)
            .ThenByDescending(command =>
                _units[command.ActorId].Stats.Agility)
            .ThenBy(command => command.ActorId.Value, StringComparer.Ordinal)
            .ToArray();

        _operations.Clear();
        _afterActionFlushStarted = false;
        _endTurnFlushStarted = false;
        _operations.AddLast(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionTrigger.TurnStarted
        )));
        foreach (var command in ordered)
        {
            _operations.AddLast(new ExecuteActionOperation(command));
        }
        _operations.AddLast(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionTrigger.TurnEnded
        )));
        _operations.AddLast(new FlushEndTurnReactionsOperation());
        _operations.AddLast(new ExpireStatusesOperation());
        _operations.AddLast(new FinishTurnOperation());
        Phase = BattleDomainPhase.ResolvingTurn;
    }

    /// <inheritdoc />
    public BattleAdvance AdvanceResolution()
    {
        ThrowIfDisposed();
        if (Phase == BattleDomainPhase.AwaitingCuePlayback)
        {
            throw new InvalidOperationException(
                "Cue playback must be acknowledged before advancing."
            );
        }
        if (Phase == BattleDomainPhase.Completed)
        {
            return BattleAdvance.Complete(
                _result ?? throw new InvalidOperationException(
                    "Completed battle has no result."
                )
            );
        }
        if (Phase == BattleDomainPhase.SelectingCommands)
        {
            return BattleAdvance.RequireCommand(
                FindRequestedPlayer()
                ?? throw new InvalidOperationException(
                    "All player commands are already selected."
                )
            );
        }
        if (Phase != BattleDomainPhase.ResolvingTurn)
        {
            throw new InvalidOperationException("Battle is not running.");
        }

        while (_operations.First is not null)
        {
            var operation = _operations.First.Value;
            _operations.RemoveFirst();

            if (operation is CueOperation cue)
            {
                _awaitingCueBatchId = ++_nextCueBatchId;
                Phase = BattleDomainPhase.AwaitingCuePlayback;
                return BattleAdvance.RequireCuePlayback(
                    _awaitingCueBatchId,
                    cue.Cues
                );
            }

            Execute(operation);
            if (Phase == BattleDomainPhase.Completed)
            {
                return BattleAdvance.Complete(_result!);
            }
            if (Phase == BattleDomainPhase.SelectingCommands)
            {
                return BattleAdvance.RequireCommand(
                    FindRequestedPlayer()!.Value
                );
            }
        }

        throw new InvalidOperationException(
            "Resolution ended without a terminal operation."
        );
    }

    /// <inheritdoc />
    public void AcknowledgeCuePlayback(long cueBatchId)
    {
        ThrowIfDisposed();
        if (
            Phase != BattleDomainPhase.AwaitingCuePlayback
            || cueBatchId != _awaitingCueBatchId
        )
        {
            throw new InvalidOperationException(
                "Cue playback acknowledgement does not match."
            );
        }

        _awaitingCueBatchId = 0;
        Phase = BattleDomainPhase.ResolvingTurn;
    }

    public BattleAdvance Flee()
    {
        ThrowIfDisposed();
        if (
            Phase is BattleDomainPhase.Disabled
                or BattleDomainPhase.Completed
        )
        {
            throw new InvalidOperationException(
                "Cannot flee from the current battle phase."
            );
        }

        Complete(BattleOutcome.Fled);
        return BattleAdvance.Complete(_result!);
    }

    public BattleSnapshot Snapshot() => new(
        Turn,
        Phase,
        _units.Values
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => unit.View())
            .ToArray()
    );

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Reset();
        _disposed = true;
    }

    private void Execute(Operation operation)
    {
        switch (operation)
        {
            case ExecuteActionOperation execute:
                ExecuteAction(execute.Command);
                break;
            case WindowOperation window:
                TriggerReactions(window.Event);
                break;
            case DamageOperation damage:
                ApplyDamage(damage);
                break;
            case HealOperation heal:
                ApplyHealing(heal);
                break;
            case ModifyResourceOperation modify:
                ModifyResource(modify);
                break;
            case ApplyStatusOperation apply:
                ApplyStatus(apply);
                break;
            case RemoveStatusOperation remove:
                RemoveStatus(remove);
                break;
            case RegisterReactionOperation register:
                RegisterReaction(register);
                break;
            case UnregisterStatusReactionsOperation unregister:
                UnregisterStatusReactions(
                    unregister.OwnerId,
                    unregister.StatusId
                );
                break;
            case ActionEndOperation actionEnd:
                EndAction(actionEnd);
                break;
            case FlushAfterActionReactionsOperation:
                _afterActionFlushStarted = true;
                FlushReactions(_afterActionReactions, repeat: false);
                break;
            case FlushEndTurnReactionsOperation:
                _endTurnFlushStarted = true;
                FlushReactions(_endTurnReactions, repeat: true);
                break;
            case ExpireStatusesOperation:
                ExpireStatuses();
                break;
            case DeathCheckOperation deathCheck:
                CheckDeaths(deathCheck);
                break;
            case FinishTurnOperation:
                FinishTurn();
                break;
            case CompleteOperation complete:
                Complete(complete.Outcome);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown battle operation '{operation.GetType().Name}'."
                );
        }
    }

    private void ExecuteAction(BattleCommand command)
    {
        _afterActionFlushStarted = false;
        if (
            !_units.TryGetValue(command.ActorId, out var actor)
            || !actor.IsAlive
            || !Catalog.TryGetAction(command.ActionId, out var action)
            || actor.Tp < action.TpCost
        )
        {
            return;
        }

        if (actor.PreventsAction(Catalog))
        {
            return;
        }

        var targets = ResolveTargets(actor, action, command.TargetId);
        if (targets.Count == 0)
        {
            return;
        }

        actor.Tp -= action.TpCost;
        var context = new EffectContext(
            actor.Id,
            targets.Select(target => target.Id).ToArray(),
            action.Range,
            action.Id,
            0,
            null,
            0
        );
        var operations = new List<Operation>
        {
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.ActionStarted,
                actor.Id,
                targets[0].Id,
                action.Id,
                Depth: 0
            )),
        };
        foreach (var effect in action.Effects)
        {
            operations.AddRange(BuildEffectOperations(effect, context));
        }
        operations.Add(new ActionEndOperation(context));
        InsertFront(operations);
    }

    private IReadOnlyList<Operation> BuildEffectOperations(
        BattleEffectDefinition effect,
        EffectContext context
    )
    {
        var operations = new List<Operation>
        {
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.BeforeEffect,
                context.SourceId,
                context.TargetIds.FirstOrDefault(),
                context.ActionId,
                Depth: context.ReactionDepth
            )),
        };

        switch (effect)
        {
            case DamageEffectDefinition damage:
                var scaledDamage = damage.Spec with
                {
                    Power = ScaleAmount(
                        context,
                        damage.Spec.Power,
                        damage.Scale
                    ),
                };
                if (!string.IsNullOrWhiteSpace(damage.AnimationId))
                {
                    operations.Add(new CueOperation([
                        new AnimationCue(
                            damage.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new DamageOperation(context, scaledDamage));
                break;
            case HealEffectDefinition heal:
                if (!string.IsNullOrWhiteSpace(heal.AnimationId))
                {
                    operations.Add(new CueOperation([
                        new AnimationCue(
                            heal.AnimationId,
                            context.SourceId,
                            context.TargetIds
                        ),
                    ]));
                }
                operations.Add(new HealOperation(
                    context,
                    ScaleAmount(context, heal.Amount, heal.Scale)
                ));
                break;
            case ModifyResourceEffectDefinition modify:
                operations.Add(new ModifyResourceOperation(
                    context,
                    modify.Resource,
                    ScaleAmount(
                        context,
                        modify.Amount,
                        modify.Scale
                    )
                ));
                break;
            case ApplyStatusEffectDefinition apply:
                operations.Add(new ApplyStatusOperation(context, apply));
                break;
            case RemoveStatusEffectDefinition remove:
                operations.Add(new RemoveStatusOperation(
                    context,
                    remove.StatusId
                ));
                break;
            case PlayAnimationEffectDefinition animation:
                operations.Add(new CueOperation([
                    new AnimationCue(
                        animation.AnimationId,
                        context.SourceId,
                        context.TargetIds
                    ),
                ]));
                break;
            case WaitEffectDefinition wait:
                operations.Add(new CueOperation([
                    new WaitCue(Math.Max(0, wait.Seconds)),
                ]));
                break;
            case RegisterReactionEffectDefinition register:
                operations.Add(new RegisterReactionOperation(
                    context,
                    register.ReactionId
                ));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported effect '{effect.GetType().Name}'."
                );
        }

        operations.Add(new WindowOperation(new ReactionEvent(
            NextCauseId(),
            ReactionTrigger.AfterEffect,
            context.SourceId,
            context.TargetIds.FirstOrDefault(),
            context.ActionId,
            Depth: context.ReactionDepth
        )));
        return operations;
    }

    private void ApplyDamage(DamageOperation operation)
    {
        if (!_units.TryGetValue(operation.Context.SourceId, out var source))
        {
            return;
        }

        var popups = new List<BattlePopup>();
        var reactionEvents = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            var damage = ComputeDamage(
                source,
                target,
                operation.Spec,
                operation.Context.Range
            );
            target.Hp = Math.Max(0, target.Hp - damage);
            popups.Add(new BattlePopup(
                target.Id,
                damage,
                BattlePopupKind.Damage
            ));
            reactionEvents.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.Damage,
                source.Id,
                target.Id,
                operation.Context.ActionId,
                Depth: operation.Context.ReactionDepth
            )));
        }

        var followUps = new List<Operation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([new PopupBatchCue(popups)]));
        }
        followUps.AddRange(reactionEvents);
        followUps.Add(new DeathCheckOperation(
            operation.Context.SourceId,
            operation.Context.ActionId,
            operation.Context.ReactionDepth
        ));
        InsertFront(followUps);
    }

    private void ApplyHealing(HealOperation operation)
    {
        var popups = new List<BattlePopup>();
        var reactionEvents = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            var amount = Math.Max(0, operation.Amount);
            var oldHp = target.Hp;
            target.Hp = Math.Min(target.Stats.MaxHp, target.Hp + amount);
            var healed = target.Hp - oldHp;
            popups.Add(new BattlePopup(
                target.Id,
                healed,
                BattlePopupKind.Heal
            ));
            reactionEvents.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.Healing,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ActionId,
                Depth: operation.Context.ReactionDepth
            )));
        }

        var followUps = new List<Operation>();
        if (popups.Count > 0)
        {
            followUps.Add(new CueOperation([new PopupBatchCue(popups)]));
        }
        followUps.AddRange(reactionEvents);
        InsertFront(followUps);
    }

    private void ModifyResource(ModifyResourceOperation operation)
    {
        var popups = new List<BattlePopup>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target))
            {
                continue;
            }
            if (operation.Resource == BattleResource.Hp)
            {
                var old = target.Hp;
                target.Hp = Math.Clamp(
                    target.Hp + operation.Amount,
                    0,
                    target.Stats.MaxHp
                );
                var delta = target.Hp - old;
                popups.Add(new BattlePopup(
                    target.Id,
                    Math.Abs(delta),
                    delta >= 0
                        ? BattlePopupKind.Heal
                        : BattlePopupKind.Damage
                ));
            }
            else
            {
                var old = target.Tp;
                target.Tp = Math.Clamp(
                    target.Tp + operation.Amount,
                    0,
                    target.Stats.MaxTp
                );
                popups.Add(new BattlePopup(
                    target.Id,
                    target.Tp - old,
                    BattlePopupKind.Tp
                ));
            }
        }
        if (popups.Count > 0)
        {
            InsertFront([
                new CueOperation([new PopupBatchCue(popups)]),
                new DeathCheckOperation(
                    operation.Context.SourceId,
                    operation.Context.ActionId,
                    operation.Context.ReactionDepth
                ),
            ]);
        }
    }

    private void ApplyStatus(ApplyStatusOperation operation)
    {
        var definition = Catalog.GetStatus(operation.Effect.StatusId);
        var followUps = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (!_units.TryGetValue(targetId, out var target) || !target.IsAlive)
            {
                continue;
            }
            target.StatusResistances.TryGetValue(
                definition.Id,
                out var resistance
            );
            target.StatusWeaknesses.TryGetValue(
                definition.Id,
                out var weakness
            );
            var chance = Math.Clamp(
                operation.Effect.BaseChance
                    * (1.0 - resistance)
                    * (1.0 + weakness),
                0.0,
                1.0
            );
            if (_random.NextDouble() >= chance)
            {
                continue;
            }

            var duration = operation.Effect.Duration > 0
                ? operation.Effect.Duration
                : definition.DefaultDuration;
            var isNew = !target.Statuses.TryGetValue(
                definition.Id,
                out var status
            );
            if (!isNew)
            {
                var existingStatus = status!;
                existingStatus.Stacks = Math.Min(
                    definition.MaxStacks,
                    existingStatus.Stacks
                        + Math.Max(1, operation.Effect.Stacks)
                );
                existingStatus.RemainingTurns = Math.Max(
                    existingStatus.RemainingTurns,
                    duration
                );
                status = existingStatus;
            }
            else
            {
                status = new RuntimeStatus(
                    definition.Id,
                    Math.Min(
                        definition.MaxStacks,
                        Math.Max(1, operation.Effect.Stacks)
                    ),
                    duration
                );
                target.Statuses.Add(definition.Id, status);
                RegisterStatusReactions(target.Id, definition);
            }

            followUps.Add(new CueOperation([
                new StatusCue(
                    target.Id,
                    definition.Id,
                    Applied: true,
                    status.Stacks
                ),
            ]));
            followUps.Add(new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.StatusApplied,
                operation.Context.SourceId,
                target.Id,
                operation.Context.ActionId,
                definition.Id,
                status.Stacks,
                operation.Context.ReactionDepth
            )));
        }
        InsertFront(followUps);
    }

    private void RemoveStatus(RemoveStatusOperation operation)
    {
        var followUps = new List<Operation>();
        foreach (var targetId in operation.Context.TargetIds)
        {
            if (
                _units.TryGetValue(targetId, out var target)
                && target.Statuses.Remove(
                    operation.StatusId,
                    out var removed
                )
            )
            {
                followUps.Add(new CueOperation([
                    new StatusCue(
                        target.Id,
                        operation.StatusId,
                        Applied: false,
                        0
                    ),
                ]));
                followUps.Add(new WindowOperation(new ReactionEvent(
                    NextCauseId(),
                    ReactionTrigger.StatusRemoved,
                    operation.Context.SourceId,
                    target.Id,
                    operation.Context.ActionId,
                    operation.StatusId,
                    removed.Stacks,
                    operation.Context.ReactionDepth
                )));
                followUps.Add(new UnregisterStatusReactionsOperation(
                    target.Id,
                    operation.StatusId
                ));
            }
        }
        InsertFront(followUps);
    }

    private void RegisterReaction(RegisterReactionOperation operation)
    {
        if (operation.Context.ReactionDepth >= MaxReactionDepth)
        {
            return;
        }
        RegisterRuntimeReaction(
            operation.Context.SourceId,
            operation.ReactionId,
            null
        );
    }

    private void TriggerReactions(ReactionEvent reactionEvent)
    {
        var matching = _reactions
            .Where(reaction =>
                reaction.Definition.Trigger == reactionEvent.Trigger
                && reaction.HasUses
                && _units.TryGetValue(reaction.OwnerId, out var owner)
                && (
                    owner.IsAlive
                    || reactionEvent.Trigger == ReactionTrigger.Defeat
                )
                && ConditionsMatch(reaction, reactionEvent))
            .OrderByDescending(reaction => reaction.Definition.Priority)
            .ThenBy(reaction => reaction.RegistrationId)
            .ToArray();

        var immediate = new List<ReactionInvocation>();
        foreach (var reaction in matching)
        {
            var guard = (reaction.RegistrationId, reactionEvent.CauseId);
            if (
                reactionEvent.Depth >= MaxReactionDepth
                || !_reactionGuards.Add(guard)
            )
            {
                continue;
            }

            reaction.Consume();
            var invocation = new ReactionInvocation(
                reaction,
                reactionEvent
            );
            switch (reaction.Definition.Schedule)
            {
                case ReactionSchedule.Immediate:
                    immediate.Add(invocation);
                    break;
                case ReactionSchedule.AfterCurrentAction:
                    if (
                        reactionEvent.ActionId is null
                        || _afterActionFlushStarted
                    )
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        _afterActionReactions.Add(invocation);
                    }
                    break;
                case ReactionSchedule.EndOfTurn:
                    if (_endTurnFlushStarted)
                    {
                        immediate.Add(invocation);
                    }
                    else
                    {
                        _endTurnReactions.Add(invocation);
                    }
                    break;
            }
        }

        FlushReactions(immediate, repeat: false);
        _reactions.RemoveAll(reaction => !reaction.HasUses);
    }

    private bool ConditionsMatch(
        RuntimeReaction reaction,
        ReactionEvent reactionEvent
    )
    {
        if (!_units.TryGetValue(reaction.OwnerId, out var owner))
        {
            return false;
        }
        foreach (var condition in reaction.Definition.Conditions)
        {
            var matches = condition switch
            {
                OwnerHasStatusConditionDefinition status =>
                    owner.Statuses.TryGetValue(
                        status.StatusId,
                        out var runtimeStatus
                    )
                    && runtimeStatus.Stacks >= status.MinimumStacks,
                TriggerActionConditionDefinition action =>
                    reactionEvent.ActionId == action.ActionId,
                TriggerStatusConditionDefinition status =>
                    reactionEvent.StatusId == status.StatusId,
                OwnerRelationConditionDefinition relation =>
                    relation.Relation switch
                    {
                        ReactionOwnerRelation.EventSource =>
                            reaction.OwnerId == reactionEvent.SourceId,
                        ReactionOwnerRelation.EventTarget =>
                            reaction.OwnerId == reactionEvent.TargetId,
                        _ => false,
                    },
                _ => false,
            };
            if (!matches)
            {
                return false;
            }
        }
        return true;
    }

    private void FlushReactions(
        List<ReactionInvocation> reactions,
        bool repeat
    )
    {
        if (reactions.Count == 0)
        {
            return;
        }

        var operations = new List<Operation>();
        foreach (var invocation in reactions)
        {
            var targets = ResolveReactionTargets(invocation);
            if (targets.Count == 0)
            {
                continue;
            }
            var context = new EffectContext(
                invocation.Reaction.OwnerId,
                targets,
                BattleRange.None,
                invocation.Event.ActionId,
                invocation.Event.Depth + 1,
                invocation.Reaction.SourceStatusId
                    ?? invocation.Event.StatusId,
                ResolveStatusStackContext(invocation)
            );
            if (
                invocation.Reaction.SourceStatusId is { } sourceStatusId
                && _units.TryGetValue(
                    invocation.Reaction.OwnerId,
                    out var owner
                )
                && owner.Statuses.TryGetValue(
                    sourceStatusId,
                    out var sourceStatus
                )
            )
            {
                operations.Add(new WindowOperation(new ReactionEvent(
                    NextCauseId(),
                    ReactionTrigger.StatusTriggered,
                    invocation.Reaction.OwnerId,
                    invocation.Reaction.OwnerId,
                    invocation.Event.ActionId,
                    sourceStatusId,
                    sourceStatus.Stacks,
                    invocation.Event.Depth + 1
                )));
            }
            foreach (var effect in invocation.Reaction.Definition.Effects)
            {
                operations.AddRange(BuildEffectOperations(effect, context));
            }
        }
        reactions.Clear();
        if (repeat && operations.Count > 0)
        {
            operations.Add(new FlushEndTurnReactionsOperation());
        }
        InsertFront(operations);
    }

    private IReadOnlyList<BattlerId> ResolveReactionTargets(
        ReactionInvocation invocation
    )
    {
        var target = invocation.Reaction.Definition.TargetPolicy switch
        {
            ReactionTargetPolicy.Owner => invocation.Reaction.OwnerId,
            ReactionTargetPolicy.EventSource =>
                invocation.Event.SourceId,
            ReactionTargetPolicy.EventTarget =>
                invocation.Event.TargetId,
            _ => null,
        };
        return target is { } id && _units.ContainsKey(id)
            ? [id]
            : [];
    }

    private void EndAction(ActionEndOperation operation)
    {
        InsertFront([
            new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.ActionFinished,
                operation.Context.SourceId,
                operation.Context.TargetIds.FirstOrDefault(),
                operation.Context.ActionId,
                Depth: operation.Context.ReactionDepth
            )),
            new FlushAfterActionReactionsOperation(),
        ]);
    }

    private void ExpireStatuses()
    {
        var operations = new List<Operation>();
        foreach (var unit in _units.Values)
        {
            foreach (var status in unit.Statuses.Values.ToArray())
            {
                status.RemainingTurns--;
                if (status.RemainingTurns > 0)
                {
                    continue;
                }
                unit.Statuses.Remove(status.Id);
                operations.Add(new CueOperation([
                    new StatusCue(
                        unit.Id,
                        status.Id,
                        Applied: false,
                        0
                    ),
                ]));
                operations.Add(new WindowOperation(new ReactionEvent(
                    NextCauseId(),
                    ReactionTrigger.StatusRemoved,
                    unit.Id,
                    unit.Id,
                    StatusId: status.Id,
                    StatusStacks: status.Stacks
                )));
                operations.Add(new UnregisterStatusReactionsOperation(
                    unit.Id,
                    status.Id
                ));
            }
        }
        InsertFront(operations);
    }

    private void CheckDeaths(DeathCheckOperation operation)
    {
        var newlyDead = _units.Values
            .Where(unit => !unit.IsAlive && _handledDeaths.Add(unit.Id))
            .OrderBy(unit => unit.Team)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (newlyDead.Length == 0)
        {
            return;
        }

        var operations = new List<Operation>
        {
            new CueOperation(
                newlyDead.Select(unit => (BattleCue)new DeathCue(unit.Id))
                    .ToArray()
            ),
        };
        operations.AddRange(newlyDead.Select(unit =>
            (Operation)new WindowOperation(new ReactionEvent(
                NextCauseId(),
                ReactionTrigger.Defeat,
                operation.SourceId,
                unit.Id,
                operation.ActionId,
                Depth: operation.ReactionDepth
            ))
        ));

        var outcome = DetermineOutcome();
        if (outcome is not null)
        {
            _operations.Clear();
            operations.Add(new CompleteOperation(outcome.Value));
        }
        InsertFront(operations);
    }

    private void FinishTurn()
    {
        var outcome = DetermineOutcome();
        if (outcome is not null)
        {
            Complete(outcome.Value);
            return;
        }

        _playerCommands.Clear();
        _commandOrder.Clear();
        _reactionGuards.Clear();
        _afterActionFlushStarted = false;
        _endTurnFlushStarted = false;
        Turn++;
        Phase = BattleDomainPhase.SelectingCommands;
    }

    private void Complete(BattleOutcome outcome)
    {
        var setup = _setup ?? throw new InvalidOperationException(
            "Battle has not been started."
        );
        _operations.Clear();
        _result = new BattleResult(
            outcome,
            setup.EncounterId,
            setup.Reward,
            _units.Values
                .Where(unit => unit.Team == BattleTeam.Player)
                .ToDictionary(unit => unit.Id, unit => (unit.Hp, unit.Tp))
        );
        Phase = BattleDomainPhase.Completed;
    }

    private bool IsValidEnemyCommand(
        BattleCommand command,
        BattleUnit enemy
    )
    {
        if (
            command.ActorId != enemy.Id
            || !Catalog.TryGetAction(command.ActionId, out var action)
            || !enemy.ActionIds.Contains(command.ActionId)
            || enemy.Tp < action.TpCost
        )
        {
            return false;
        }

        return ResolveTargets(enemy, action, command.TargetId).Count > 0;
    }

    private BattleOutcome? DetermineOutcome()
    {
        var playersAlive = _units.Values.Any(unit =>
            unit.Team == BattleTeam.Player && unit.IsAlive);
        var enemiesAlive = _units.Values.Any(unit =>
            unit.Team == BattleTeam.Enemy && unit.IsAlive);
        if (!enemiesAlive)
        {
            return BattleOutcome.Victory;
        }
        if (!playersAlive)
        {
            return BattleOutcome.Defeat;
        }
        return null;
    }

    private void RegisterStatusReactions(
        BattlerId ownerId,
        StatusDefinition status
    )
    {
        foreach (var reactionId in status.ReactionIds)
        {
            RegisterRuntimeReaction(ownerId, reactionId, status.Id);
        }
    }

    private void RegisterRuntimeReaction(
        BattlerId ownerId,
        ReactionId reactionId,
        StatusId? sourceStatusId
    )
    {
        _reactions.Add(new RuntimeReaction(
            ++_nextReactionRegistrationId,
            ownerId,
            Catalog.GetReaction(reactionId),
            sourceStatusId
        ));
    }

    private void UnregisterStatusReactions(
        BattlerId ownerId,
        StatusId statusId
    ) => _reactions.RemoveAll(reaction =>
        reaction.OwnerId == ownerId
        && reaction.SourceStatusId == statusId);

    private int ResolveStatusStackContext(
        ReactionInvocation invocation
    )
    {
        var statusId = invocation.Reaction.SourceStatusId
            ?? invocation.Event.StatusId;
        if (
            statusId is { } id
            && _units.TryGetValue(
                invocation.Reaction.OwnerId,
                out var owner
            )
            && owner.Statuses.TryGetValue(id, out var status)
        )
        {
            return status.Stacks;
        }
        return invocation.Event.StatusId == statusId
            ? invocation.Event.StatusStacks
            : 0;
    }

    private int ScaleAmount(
        EffectContext context,
        int amount,
        StatusStackScaleDefinition? scale
    ) => (int)Math.Round(ScaleAmount(context, (double)amount, scale));

    private double ScaleAmount(
        EffectContext context,
        double amount,
        StatusStackScaleDefinition? scale
    )
    {
        if (scale is null)
        {
            return amount;
        }
        if (
            _units.TryGetValue(context.SourceId, out var source)
            && source.Statuses.TryGetValue(scale.StatusId, out var status)
        )
        {
            return amount * status.Stacks;
        }
        return context.StatusId == scale.StatusId
            ? amount * context.StatusStacks
            : 0;
    }

    private int ComputeDamage(
        BattleUnit source,
        BattleUnit target,
        DamageSpec spec,
        BattleRange range
    )
    {
        double damage;
        if (spec.Mode == DamageMode.Fixed)
        {
            damage = spec.Power;
        }
        else if (spec.Type == DamageType.True)
        {
            damage = spec.Power
                + source.Stats.Strength
                + source.Stats.Technique
                + source.Stats.Attack;
        }
        else if (
            spec.Type is DamageType.Fire
                or DamageType.Ice
                or DamageType.Lightning
        )
        {
            var offense = spec.Power
                + source.Stats.Technique
                + (source.Stats.Attack * 0.5);
            var defense = (target.Stats.Wisdom * 1.35)
                + (target.Stats.Technique * 0.65)
                + (target.Stats.Defense * 0.35);
            damage = offense * (offense / (offense + defense + 1.0));
        }
        else
        {
            var offense = spec.Power
                + source.Stats.Strength
                + source.Stats.Attack;
            var defense = target.Stats.Defense
                + (target.Stats.Vitality * 0.75);
            damage = offense * (offense / (offense + defense + 1.0));
        }

        if (
            range == BattleRange.Melee
            && (
                source.Position.Row == PartyRow.Back
                || target.Position.Row == PartyRow.Back
            )
        )
        {
            damage *= BackRowMeleeMultiplier;
        }

        if (spec.CanCrit)
        {
            var chance = Math.Clamp(
                0.05
                    + ((source.Stats.Luck - target.Stats.Luck) * 0.0025),
                0.01,
                0.5
            );
            if (_random.NextDouble() < chance)
            {
                damage *= spec.CritMultiplier;
            }
        }

        if (spec.Type != DamageType.True)
        {
            target.DamageTypeResistances.TryGetValue(
                spec.Type,
                out var resistance
            );
            target.DamageTypeWeaknesses.TryGetValue(
                spec.Type,
                out var weakness
            );
            damage *= (1.0 - resistance) * (1.0 + weakness);
        }

        return Math.Max(0, (int)Math.Round(damage));
    }

    private List<BattleUnit> ResolveTargets(
        BattleUnit source,
        BattleActionDefinition action,
        BattlerId? selectedId
    )
    {
        var candidates = GetTargetCandidates(source, action);
        if (candidates.Count == 0)
        {
            return [];
        }

        if (action.TargetRule == BattleTargetRule.Self)
        {
            return [source];
        }
        if (
            action.TargetRule is BattleTargetRule.AllAllies
                or BattleTargetRule.AllEnemies
        )
        {
            return candidates;
        }

        var selected = selectedId is { } id
            ? candidates.FirstOrDefault(unit => unit.Id == id)
            : null;
        if (selected is null)
        {
            if (action.RetargetPolicy == RetargetPolicy.Fail)
            {
                return [];
            }
            selected = candidates[0];
        }

        if (
            action.TargetRule is BattleTargetRule.RowAllies
                or BattleTargetRule.RowEnemies
        )
        {
            return candidates
                .Where(unit => unit.Position.Row == selected.Position.Row)
                .ToList();
        }
        return [selected];
    }

    private List<BattleUnit> GetTargetCandidates(
        BattleUnit source,
        BattleActionDefinition action
    )
    {
        var team = action.TargetRule switch
        {
            BattleTargetRule.Self => source.Team,
            BattleTargetRule.SingleAlly => source.Team,
            BattleTargetRule.RowAllies => source.Team,
            BattleTargetRule.AllAllies => source.Team,
            _ => source.Team == BattleTeam.Player
                ? BattleTeam.Enemy
                : BattleTeam.Player,
        };

        return _units.Values
            .Where(unit =>
                unit.IsAlive
                && unit.Team == team
                && (
                    action.TargetRule != BattleTargetRule.Self
                    || unit.Id == source.Id
                ))
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
    }

    private BattlerId? FindRequestedPlayer()
    {
        foreach (var unit in OrderedUnits(BattleTeam.Player))
        {
            if (unit.IsAlive && !_playerCommands.ContainsKey(unit.Id))
            {
                return unit.Id;
            }
        }
        return null;
    }

    private IEnumerable<BattleUnit> OrderedUnits(BattleTeam team) =>
        _units.Values
            .Where(unit => unit.Team == team)
            .OrderBy(unit => unit.Position.Row)
            .ThenBy(unit => unit.Position.Index)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal);

    private void InsertFront(IEnumerable<Operation> operations)
    {
        foreach (var operation in operations.Reverse())
        {
            _operations.AddFirst(operation);
        }
    }

    private long NextCauseId() => ++_nextCauseId;

    private void Reset()
    {
        _units.Clear();
        _playerCommands.Clear();
        _commandOrder.Clear();
        _operations.Clear();
        _afterActionReactions.Clear();
        _endTurnReactions.Clear();
        _reactions.Clear();
        _reactionGuards.Clear();
        _handledDeaths.Clear();
        _setup = null;
        _result = null;
        _nextCueBatchId = 0;
        _awaitingCueBatchId = 0;
        _nextCauseId = 0;
        _nextReactionRegistrationId = 0;
        _afterActionFlushStarted = false;
        _endTurnFlushStarted = false;
        Turn = 0;
        Phase = BattleDomainPhase.Disabled;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class BattleUnit
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
            ReactionIds = seed.ReactionIds.ToArray();
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
        public IReadOnlyList<ReactionId> ReactionIds { get; }
        public Dictionary<StatusId, double> StatusResistances { get; }
        public Dictionary<StatusId, double> StatusWeaknesses { get; }
        public Dictionary<DamageType, double> DamageTypeResistances { get; }
        public Dictionary<DamageType, double> DamageTypeWeaknesses { get; }
        public Dictionary<StatusId, RuntimeStatus> Statuses { get; } = [];
        public bool IsAlive => Hp > 0;

        public bool PreventsAction(
            BattleCatalog catalog
        ) => Statuses.Keys.Any(id =>
            catalog.GetStatus(id).PreventsAction);

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

    private sealed class RuntimeStatus(
        StatusId id,
        int stacks,
        int remainingTurns
    )
    {
        public StatusId Id { get; } = id;
        public int Stacks { get; set; } = stacks;
        public int RemainingTurns { get; set; } = remainingTurns;
    }

    private sealed class RuntimeReaction(
        long registrationId,
        BattlerId ownerId,
        ReactionDefinition definition,
        StatusId? sourceStatusId
    )
    {
        public long RegistrationId { get; } = registrationId;
        public BattlerId OwnerId { get; } = ownerId;
        public ReactionDefinition Definition { get; } = definition;
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

    private sealed record EffectContext(
        BattlerId SourceId,
        IReadOnlyList<BattlerId> TargetIds,
        BattleRange Range,
        ActionId? ActionId,
        int ReactionDepth,
        StatusId? StatusId,
        int StatusStacks
    );

    private sealed record ReactionEvent(
        long CauseId,
        ReactionTrigger Trigger,
        BattlerId? SourceId = null,
        BattlerId? TargetId = null,
        ActionId? ActionId = null,
        StatusId? StatusId = null,
        int StatusStacks = 0,
        int Depth = 0
    );

    private sealed record ReactionInvocation(
        RuntimeReaction Reaction,
        ReactionEvent Event
    );

    private abstract record Operation;
    private sealed record CueOperation(IReadOnlyList<BattleCue> Cues)
        : Operation;
    private sealed record ExecuteActionOperation(BattleCommand Command)
        : Operation;
    private sealed record WindowOperation(ReactionEvent Event) : Operation;
    private sealed record DamageOperation(
        EffectContext Context,
        DamageSpec Spec
    ) : Operation;
    private sealed record HealOperation(
        EffectContext Context,
        int Amount
    ) : Operation;
    private sealed record ModifyResourceOperation(
        EffectContext Context,
        BattleResource Resource,
        int Amount
    ) : Operation;
    private sealed record ApplyStatusOperation(
        EffectContext Context,
        ApplyStatusEffectDefinition Effect
    ) : Operation;
    private sealed record RemoveStatusOperation(
        EffectContext Context,
        StatusId StatusId
    ) : Operation;
    private sealed record RegisterReactionOperation(
        EffectContext Context,
        ReactionId ReactionId
    ) : Operation;
    private sealed record UnregisterStatusReactionsOperation(
        BattlerId OwnerId,
        StatusId StatusId
    ) : Operation;
    private sealed record ActionEndOperation(EffectContext Context)
        : Operation;
    private sealed record FlushAfterActionReactionsOperation : Operation;
    private sealed record FlushEndTurnReactionsOperation : Operation;
    private sealed record ExpireStatusesOperation : Operation;
    private sealed record DeathCheckOperation(
        BattlerId? SourceId,
        ActionId? ActionId,
        int ReactionDepth
    ) : Operation;
    private sealed record FinishTurnOperation : Operation;
    private sealed record CompleteOperation(BattleOutcome Outcome)
        : Operation;
}
